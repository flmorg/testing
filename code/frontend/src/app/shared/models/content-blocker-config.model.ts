export enum ScheduleUnit {
  Seconds = 'Seconds',
  Minutes = 'Minutes',
  Hours = 'Hours'
}

/**
 * Valid values for each schedule unit
 */
export const ScheduleOptions = {
  [ScheduleUnit.Seconds]: [5, 10, 15, 30],
  [ScheduleUnit.Minutes]: [1, 2, 3, 4, 5, 6, 10, 12, 15, 20, 30],
  [ScheduleUnit.Hours]: [1, 2, 3, 4, 6]
};

export interface JobSchedule {
  every: number;
  type: ScheduleUnit;
}

export enum BlocklistType {
  Blacklist = 'Blacklist',
  Whitelist = 'Whitelist'
}

export interface BlocklistSettings {
  enabled: boolean;
  blocklistPath: string;
  blocklistType: BlocklistType;
}

export interface ContentBlockerConfig {
  enabled: boolean;
  cronExpression: string;
  useAdvancedScheduling: boolean;
  jobSchedule?: JobSchedule; // UI-only field, not sent to API
  
  ignorePrivate: boolean;
  deletePrivate: boolean;
  
  sonarr: BlocklistSettings;
  radarr: BlocklistSettings;
  lidarr: BlocklistSettings;
} 