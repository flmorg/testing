import { inject, Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { LogEntry } from '../models/signalr.models';
import { AppEvent } from '../models/event.models';
import { BasePathService } from './base-path.service';

/**
 * Unified SignalR hub service
 */
@Injectable({
  providedIn: 'root'
})
export class AppHubService {
  private hubConnection!: signalR.HubConnection;
  private connectionStatusSubject = new BehaviorSubject<boolean>(false);
  private logsSubject = new BehaviorSubject<LogEntry[]>([]);
  private eventsSubject = new BehaviorSubject<AppEvent[]>([]);
  private readonly basePathService = inject(BasePathService);
  
  private logBuffer: LogEntry[] = [];
  private eventBuffer: AppEvent[] = [];
  private readonly bufferSize = 1000;

  constructor() { }
  
  /**
   * Start the SignalR connection
   */
  public startConnection(): Promise<void> {
    if (this.hubConnection && 
        this.hubConnection.state !== signalR.HubConnectionState.Disconnected) {
      return Promise.resolve();
    }

    // Build a new connection
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.basePathService.buildApiUrl('/hubs/app'))
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Implement exponential backoff with max 30 seconds
          return Math.min(2000 * Math.pow(2, retryContext.previousRetryCount), 30000);
        }
      })
      .build();

    this.registerSignalREvents();

    return this.hubConnection.start()
      .then(() => {
        console.log('AppHub connection started');
        this.connectionStatusSubject.next(true);
        this.requestInitialData();
      })
      .catch(err => {
        console.error('Error connecting to AppHub:', err);
        this.connectionStatusSubject.next(false);
        throw err;
      });
  }
  
  /**
   * Register SignalR event handlers
   */
  private registerSignalREvents(): void {
    // Handle connection events
    this.hubConnection.onreconnected(() => {
      console.log('AppHub reconnected');
      this.connectionStatusSubject.next(true);
      this.requestInitialData();
    });

    this.hubConnection.onreconnecting(() => {
      console.log('AppHub reconnecting...');
      this.connectionStatusSubject.next(false);
    });

    this.hubConnection.onclose(() => {
      console.log('AppHub connection closed');
      this.connectionStatusSubject.next(false);
    });

    // Handle individual log messages
    this.hubConnection.on('LogReceived', (log: LogEntry) => {
      this.addLogToBuffer(log);
      const currentLogs = this.logsSubject.value;
      this.logsSubject.next([...currentLogs, log]);
    });
    
    // Handle bulk log messages (initial load)
    this.hubConnection.on('LogsReceived', (logs: LogEntry[]) => {
      if (logs && logs.length > 0) {
        // Set all logs at once
        this.logsSubject.next(logs);
        // Update buffer
        this.logBuffer = [...logs];
        this.trimBuffer(this.logBuffer, this.bufferSize);
      }
    });
    
    // Handle individual event messages
    this.hubConnection.on('EventReceived', (event: AppEvent) => {
      this.addEventToBuffer(event);
      const currentEvents = this.eventsSubject.value;
      this.eventsSubject.next([...currentEvents, event]);
    });
    
    // Handle bulk event messages (initial load)
    this.hubConnection.on('EventsReceived', (events: AppEvent[]) => {
      if (events && events.length > 0) {
        // Set all events at once
        this.eventsSubject.next(events);
        // Update buffer
        this.eventBuffer = [...events];
        this.trimBuffer(this.eventBuffer, this.bufferSize);
      }
    });
  }
  
  /**
   * Request initial data from the server
   */
  private requestInitialData(): void {
    this.requestRecentLogs();
    this.requestRecentEvents();
  }
  
  /**
   * Request recent logs from the server
   */
  public requestRecentLogs(): void {
    if (this.isConnected()) {
      this.hubConnection.invoke('GetRecentLogs')
        .catch(err => console.error('Error requesting recent logs:', err));
    }
  }
  
  /**
   * Request recent events from the server
   */
  public requestRecentEvents(count: number = 100): void {
    if (this.isConnected()) {
      this.hubConnection.invoke('GetRecentEvents', count)
        .catch(err => console.error('Error requesting recent events:', err));
    }
  }
  
  /**
   * Check if the connection is established
   */
  private isConnected(): boolean {
    return this.hubConnection && 
           this.hubConnection.state === signalR.HubConnectionState.Connected;
  }
  
  /**
   * Stop the SignalR connection
   */
  public stopConnection(): Promise<void> {
    if (!this.hubConnection) {
      return Promise.resolve();
    }
    
    return this.hubConnection.stop()
      .then(() => {
        console.log('AppHub connection stopped');
        this.connectionStatusSubject.next(false);
      })
      .catch(err => {
        console.error('Error stopping AppHub connection:', err);
        throw err;
      });
  }
  
  /**
   * Add a log to the buffer
   */
  private addLogToBuffer(log: LogEntry): void {
    this.logBuffer.push(log);
    this.trimBuffer(this.logBuffer, this.bufferSize);
  }
  
  /**
   * Add an event to the buffer
   */
  private addEventToBuffer(event: AppEvent): void {
    this.eventBuffer.push(event);
    this.trimBuffer(this.eventBuffer, this.bufferSize);
  }
  
  /**
   * Trim a buffer to the specified size
   */
  private trimBuffer<T>(buffer: T[], maxSize: number): void {
    while (buffer.length > maxSize) {
      buffer.shift();
    }
  }
  
  // PUBLIC API METHODS
  
  /**
   * Get logs as an observable
   */
  public getLogs(): Observable<LogEntry[]> {
    return this.logsSubject.asObservable();
  }
  
  /**
   * Get events as an observable
   */
  public getEvents(): Observable<AppEvent[]> {
    return this.eventsSubject.asObservable();
  }
  
  /**
   * Get connection status as an observable
   */
  public getConnectionStatus(): Observable<boolean> {
    return this.connectionStatusSubject.asObservable();
  }
  
  /**
   * Get logs connection status as an observable
   * For backward compatibility with components expecting separate connection statuses
   */
  public getLogsConnectionStatus(): Observable<boolean> {
    return this.connectionStatusSubject.asObservable();
  }
  
  /**
   * Get events connection status as an observable
   * For backward compatibility with components expecting separate connection statuses
   */
  public getEventsConnectionStatus(): Observable<boolean> {
    return this.connectionStatusSubject.asObservable();
  }
  
  /**
   * Clear events
   */
  public clearEvents(): void {
    this.eventsSubject.next([]);
    this.eventBuffer = [];
  }
  
  /**
   * Clear logs
   */
  public clearLogs(): void {
    this.logsSubject.next([]);
    this.logBuffer = [];
  }
}
