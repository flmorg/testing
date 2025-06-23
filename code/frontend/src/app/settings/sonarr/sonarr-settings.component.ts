import { Component, EventEmitter, OnDestroy, Output, effect, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from "@angular/forms";
import { Subject, takeUntil } from "rxjs";
import { SonarrConfigStore } from "./sonarr-config.store";
import { CanComponentDeactivate } from "../../core/guards";
import { SonarrConfig } from "../../shared/models/sonarr-config.model";
import { CreateArrInstanceDto, ArrInstance } from "../../shared/models/arr-config.model";

// PrimeNG Components
import { CardModule } from "primeng/card";
import { InputTextModule } from "primeng/inputtext";
import { CheckboxModule } from "primeng/checkbox";
import { ButtonModule } from "primeng/button";
import { InputNumberModule } from "primeng/inputnumber";
import { ToastModule } from "primeng/toast";
import { DialogModule } from "primeng/dialog";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { ConfirmationService } from "primeng/api";
import { NotificationService } from "../../core/services/notification.service";
import { LoadingErrorStateComponent } from "../../shared/components/loading-error-state/loading-error-state.component";

@Component({
  selector: "app-sonarr-settings",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CardModule,
    InputTextModule,
    CheckboxModule,
    ButtonModule,
    InputNumberModule,
    ToastModule,
    DialogModule,
    ConfirmDialogModule,
    LoadingErrorStateComponent,
  ],
  providers: [SonarrConfigStore, ConfirmationService],
  templateUrl: "./sonarr-settings.component.html",
  styleUrls: ["./sonarr-settings.component.scss"],
})
export class SonarrSettingsComponent implements OnDestroy, CanComponentDeactivate {
  @Output() saved = new EventEmitter<void>();
  @Output() error = new EventEmitter<string>();

  // Forms
  globalForm: FormGroup;
  instanceForm: FormGroup;

  // Modal state
  showInstanceModal = false;
  modalMode: 'add' | 'edit' = 'add';
  editingInstance: ArrInstance | null = null;

  // Original form values for tracking changes
  private originalGlobalValues: any;
  hasGlobalChanges = false;

  // Clean up subscriptions
  private destroy$ = new Subject<void>();

  // Services
  private formBuilder = inject(FormBuilder);
  private notificationService = inject(NotificationService);
  private confirmationService = inject(ConfirmationService);
  private sonarrStore = inject(SonarrConfigStore);

  // Signals from store
  sonarrConfig = this.sonarrStore.config;
  sonarrLoading = this.sonarrStore.loading;
  sonarrError = this.sonarrStore.error;
  sonarrSaving = this.sonarrStore.saving;

  /**
   * Check if component can be deactivated (navigation guard)
   */
  canDeactivate(): boolean {
    return !this.globalForm?.dirty || !this.hasGlobalChanges;
  }

  constructor() {
    // Initialize forms
    this.globalForm = this.formBuilder.group({
      failedImportMaxStrikes: [-1],
    });

    this.instanceForm = this.formBuilder.group({
      enabled: [true],
      name: ['', Validators.required],
      url: ['', [Validators.required, this.uriValidator.bind(this)]],
      apiKey: ['', Validators.required],
    });

    // Load Sonarr config data
    this.sonarrStore.loadConfig();

    // Setup effect to update form when config changes
    effect(() => {
      const config = this.sonarrConfig();
      if (config) {
        this.updateGlobalFormFromConfig(config);
      }
    });

    // Track global form changes
    this.globalForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.hasGlobalChanges = this.globalFormValuesChanged();
      });
  }

  /**
   * Clean up subscriptions when component is destroyed
   */
  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Update global form with values from the configuration
   */
  private updateGlobalFormFromConfig(config: SonarrConfig): void {
    this.globalForm.patchValue({
      failedImportMaxStrikes: config.failedImportMaxStrikes,
    });

    // Store original values for dirty checking
    this.storeOriginalGlobalValues();
  }

  /**
   * Store original global form values for dirty checking
   */
  private storeOriginalGlobalValues(): void {
    this.originalGlobalValues = JSON.parse(JSON.stringify(this.globalForm.value));
    this.globalForm.markAsPristine();
    this.hasGlobalChanges = false;
  }

  /**
   * Check if the current global form values are different from the original values
   */
  private globalFormValuesChanged(): boolean {
    return !this.isEqual(this.globalForm.value, this.originalGlobalValues);
  }

  /**
   * Deep compare two objects for equality
   */
  private isEqual(obj1: any, obj2: any): boolean {
    if (obj1 === obj2) return true;

    if (typeof obj1 !== "object" || typeof obj2 !== "object" || obj1 == null || obj2 == null) {
      return false;
    }

    const keys1 = Object.keys(obj1);
    const keys2 = Object.keys(obj2);

    if (keys1.length !== keys2.length) return false;

    for (const key of keys1) {
      const val1 = obj1[key];
      const val2 = obj2[key];
      const areObjects = typeof val1 === "object" && typeof val2 === "object";

      if ((areObjects && !this.isEqual(val1, val2)) || (!areObjects && val1 !== val2)) {
        return false;
      }
    }

    return true;
  }

  /**
   * Custom validator to check if the input is a valid URI
   */
  private uriValidator(control: AbstractControl): ValidationErrors | null {
    if (!control.value) {
      return null; // Let required validator handle empty values
    }
    
    try {
      const url = new URL(control.value);
      
      // Check that we have a valid protocol (http or https)
      if (url.protocol !== 'http:' && url.protocol !== 'https:') {
        return { invalidProtocol: true };
      }
      
      return null; // Valid URI
    } catch (e) {
      return { invalidUri: true }; // Invalid URI
    }
  }

  /**
   * Mark all controls in a form group as touched
   */
  private markFormGroupTouched(formGroup: FormGroup): void {
    Object.values(formGroup.controls).forEach((control) => {
      control.markAsTouched();

      if ((control as any).controls) {
        this.markFormGroupTouched(control as FormGroup);
      }
    });
  }

  /**
   * Check if a form control has an error
   */
  hasError(form: FormGroup, controlName: string, errorName: string): boolean {
    const control = form.get(controlName);
    return control !== null && control.hasError(errorName) && control.touched;
  }

  /**
   * Save the global Sonarr configuration
   */
  saveGlobalConfig(): void {
    this.markFormGroupTouched(this.globalForm);

    if (this.globalForm.invalid) {
      this.notificationService.showError('Please fix the validation errors before saving');
      return;
    }

    if (!this.hasGlobalChanges) {
      this.notificationService.showSuccess('No changes detected');
      return;
    }

    const updatedConfig = {
      failedImportMaxStrikes: this.globalForm.get('failedImportMaxStrikes')?.value
    };

    this.sonarrStore.saveConfig(updatedConfig);
    
    // Monitor saving completion
    this.monitorGlobalSaving();
  }

  /**
   * Monitor global saving completion
   */
  private monitorGlobalSaving(): void {
    const checkSavingStatus = () => {
      const saving = this.sonarrSaving();
      const error = this.sonarrError();
      
      if (!saving) {
        if (error) {
          this.notificationService.showError(`Save failed: ${error}`);
          this.error.emit(error);
        } else {
          this.notificationService.showSuccess('Global configuration saved successfully');
          this.saved.emit();
          
          // Reset form state without reloading from backend
          this.globalForm.markAsPristine();
          this.hasGlobalChanges = false;
          this.storeOriginalGlobalValues();
        }
      } else {
        setTimeout(checkSavingStatus, 100);
      }
    };
    
    setTimeout(checkSavingStatus, 100);
  }

  /**
   * Get instances from current config
   */
  get instances(): ArrInstance[] {
    return this.sonarrConfig()?.instances || [];
  }

  /**
   * Open modal to add new instance
   */
  openAddInstanceModal(): void {
    this.modalMode = 'add';
    this.editingInstance = null;
    this.instanceForm.reset({
      enabled: true,
      name: '',
      url: '',
      apiKey: ''
    });
    this.showInstanceModal = true;
  }

  /**
   * Open modal to edit existing instance
   */
  openEditInstanceModal(instance: ArrInstance): void {
    this.modalMode = 'edit';
    this.editingInstance = instance;
    this.instanceForm.patchValue({
      enabled: instance.enabled,
      name: instance.name,
      url: instance.url,
      apiKey: instance.apiKey,
    });
    this.showInstanceModal = true;
  }

  /**
   * Close instance modal
   */
  closeInstanceModal(): void {
    this.showInstanceModal = false;
    this.editingInstance = null;
    this.instanceForm.reset();
  }

  /**
   * Save instance (add or edit)
   */
  saveInstance(): void {
    this.markFormGroupTouched(this.instanceForm);

    if (this.instanceForm.invalid) {
      this.notificationService.showError('Please fix the validation errors before saving');
      return;
    }

    const instanceData: CreateArrInstanceDto = {
      enabled: this.instanceForm.get('enabled')?.value,
      name: this.instanceForm.get('name')?.value,
      url: this.instanceForm.get('url')?.value,
      apiKey: this.instanceForm.get('apiKey')?.value,
    };

    if (this.modalMode === 'add') {
      this.sonarrStore.createInstance(instanceData);
    } else if (this.editingInstance) {
      this.sonarrStore.updateInstance({ 
        id: this.editingInstance.id!, 
        instance: instanceData 
      });
    }

    this.monitorInstanceSaving();
  }

  /**
   * Monitor instance saving completion
   */
  private monitorInstanceSaving(): void {
    const checkSavingStatus = () => {
      const saving = this.sonarrSaving();
      const error = this.sonarrError();
      
      if (!saving) {
        if (error) {
          this.notificationService.showError(`Operation failed: ${error}`);
        } else {
          const action = this.modalMode === 'add' ? 'created' : 'updated';
          this.notificationService.showSuccess(`Instance ${action} successfully`);
          this.closeInstanceModal();
        }
      } else {
        setTimeout(checkSavingStatus, 100);
      }
    };
    
    setTimeout(checkSavingStatus, 100);
  }

  /**
   * Delete instance with confirmation
   */
  deleteInstance(instance: ArrInstance): void {
    this.confirmationService.confirm({
      message: `Are you sure you want to delete the instance "${instance.name}"?`,
      header: 'Confirm Deletion',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.sonarrStore.deleteInstance(instance.id!);
        
        // Monitor deletion
        const checkDeletionStatus = () => {
          const saving = this.sonarrSaving();
          const error = this.sonarrError();
          
          if (!saving) {
            if (error) {
              this.notificationService.showError(`Deletion failed: ${error}`);
            } else {
              this.notificationService.showSuccess('Instance deleted successfully');
            }
          } else {
            setTimeout(checkDeletionStatus, 100);
          }
        };
        
        setTimeout(checkDeletionStatus, 100);
      }
    });
  }



  /**
   * Get modal title based on mode
   */
  get modalTitle(): string {
    return this.modalMode === 'add' ? 'Add Sonarr Instance' : 'Edit Sonarr Instance';
  }
}
