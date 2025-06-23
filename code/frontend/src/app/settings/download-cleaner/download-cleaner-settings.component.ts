import { Component, EventEmitter, OnDestroy, Output, inject, effect } from "@angular/core";
import { CommonModule, NgIf } from "@angular/common";
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from "@angular/forms";
import { Subject, takeUntil } from "rxjs";
import { DownloadCleanerConfigStore } from "./download-cleaner-config.store";
import { CanComponentDeactivate } from "../../core/guards";
import {
  CleanCategory,
  DownloadCleanerConfig,
  JobSchedule,
  createDefaultCategory
} from "../../shared/models/download-cleaner-config.model";
import { ScheduleUnit, ScheduleOptions } from "../../shared/models/queue-cleaner-config.model";

// PrimeNG Components
import { CardModule } from "primeng/card";
import { InputTextModule } from "primeng/inputtext";
import { CheckboxModule } from "primeng/checkbox";
import { ButtonModule } from "primeng/button";
import { InputNumberModule } from "primeng/inputnumber";
import { AccordionModule } from "primeng/accordion";
import { SelectButtonModule } from "primeng/selectbutton";
import { ChipsModule } from "primeng/chips";
import { ToastModule } from "primeng/toast";
import { NotificationService } from "../../core/services/notification.service";
import { SelectModule } from "primeng/select";
import { AutoCompleteModule } from "primeng/autocomplete";
import { DropdownModule } from "primeng/dropdown";
import { TableModule } from "primeng/table";
import { LoadingErrorStateComponent } from "../../shared/components/loading-error-state/loading-error-state.component";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { ConfirmationService } from "primeng/api";

@Component({
  selector: "app-download-cleaner-settings",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CardModule,
    InputTextModule,
    CheckboxModule,
    ButtonModule,
    InputNumberModule,
    AccordionModule,
    SelectButtonModule,
    ChipsModule,
    ToastModule,
    SelectModule,
    AutoCompleteModule,
    DropdownModule,
    TableModule,
    LoadingErrorStateComponent,
    ConfirmDialogModule,
    NgIf
  ],
  providers: [DownloadCleanerConfigStore, ConfirmationService],
  templateUrl: "./download-cleaner-settings.component.html",
  styleUrls: ["./download-cleaner-settings.component.scss"],
})
export class DownloadCleanerSettingsComponent implements OnDestroy, CanComponentDeactivate {
  @Output() error = new EventEmitter<string>();
  @Output() saved = new EventEmitter<void>();

  // Services
  private formBuilder = inject(FormBuilder);
  private notificationService = inject(NotificationService);
  private downloadCleanerStore = inject(DownloadCleanerConfigStore);
  private confirmationService = inject(ConfirmationService);
  
  // Configuration signals
  readonly downloadCleanerConfig = this.downloadCleanerStore.config;
  readonly downloadCleanerLoading = this.downloadCleanerStore.loading;
  readonly downloadCleanerSaving = this.downloadCleanerStore.saving;
  readonly downloadCleanerError = this.downloadCleanerStore.error;
  
  // Form and state
  downloadCleanerForm!: FormGroup;
  originalFormValues: any;
  private destroy$ = new Subject<void>();
  hasActualChanges = false; // Flag to track actual form changes
  activeAccordionIndices: number[] = [];
  
  // Track the previous enabled state to detect when user is trying to enable
  private previousEnabledState = false;
  
  // Flag to track if form has been initially loaded to avoid showing dialog on page load
  private formInitialized = false;
  
  // Minimal autocomplete support - empty suggestions to allow manual input
  unlinkedCategoriesSuggestions: string[] = [];
  
  // Get the categories form array for easier access in the template
  get categoriesFormArray(): FormArray {
    return this.downloadCleanerForm.get('categories') as FormArray;
  }
  
  // Schedule unit options for job schedules
  scheduleUnitOptions = [
    { label: "Seconds", value: ScheduleUnit.Seconds },
    { label: "Minutes", value: ScheduleUnit.Minutes },
    { label: "Hours", value: ScheduleUnit.Hours },
  ];
  
  // Options for each schedule unit
  scheduleValueOptions = {
    [ScheduleUnit.Seconds]: ScheduleOptions[ScheduleUnit.Seconds].map(v => ({ label: v.toString(), value: v })),
    [ScheduleUnit.Minutes]: ScheduleOptions[ScheduleUnit.Minutes].map(v => ({ label: v.toString(), value: v })),
    [ScheduleUnit.Hours]: ScheduleOptions[ScheduleUnit.Hours].map(v => ({ label: v.toString(), value: v }))
  };

  // Display modes for schedule
  scheduleModeOptions = [
    { label: 'Basic', value: false },
    { label: 'Advanced', value: true }
  ];

  /**
   * Check if component can be deactivated (navigation guard)
   */
  canDeactivate(): boolean {
    // Allow navigation if form is not dirty or has been saved
    return !this.downloadCleanerForm?.dirty || !this.formValuesChanged();
  }

  constructor() {
    // Initialize the form with proper disabled states for dependent controls
    this.downloadCleanerForm = this.formBuilder.group({
      enabled: [false],
      useAdvancedScheduling: [{ value: false, disabled: true }],
      cronExpression: [{ value: "0 0 * * * ?", disabled: true }, [Validators.required]],
      jobSchedule: this.formBuilder.group({
        every: [{ value: 5, disabled: true }, [Validators.required, Validators.min(1)]],
        type: [{ value: ScheduleUnit.Minutes, disabled: true }, [Validators.required]]
      }),
      categories: this.formBuilder.array([]),
      deletePrivate: [{ value: false, disabled: true }],
      unlinkedEnabled: [{ value: false, disabled: true }],
      unlinkedTargetCategory: [{ value: 'cleanuparr-unlinked', disabled: true }, [Validators.required]],
      unlinkedUseTag: [{ value: false, disabled: true }],
      unlinkedIgnoredRootDir: [{ value: '', disabled: true }],
      unlinkedCategories: [{ value: [], disabled: true }]
    }, { validators: this.validateUnlinkedCategories });

    // Load the current configuration
    effect(() => {
      const config = this.downloadCleanerConfig();
      if (config) {
        this.updateForm(config);
      }
    });
    
    // Effect to handle errors
    effect(() => {
      const errorMessage = this.downloadCleanerError();
      if (errorMessage) {
        // Only emit the error for parent components
        this.error.emit(errorMessage);
      }
    });
    
    // Set up listeners for form value changes
    this.setupFormValueChangeListeners();
  }
  
  /**
   * Add a new category to the form array
   */
  addCategory(category: CleanCategory = createDefaultCategory()): void {
    // Create a form group for the category with validation and add it to the form array
    const categoryGroup = this.createCategoryFormGroup(category);
    
    this.categoriesFormArray.push(categoryGroup);
    this.downloadCleanerForm.markAsDirty();
  }
  
  /**
   * Create a category form group with validation
   */
  private createCategoryFormGroup(category: CleanCategory): FormGroup {
    return this.formBuilder.group({
      name: [category.name, Validators.required],
      maxRatio: [category.maxRatio],
      minSeedTime: [category.minSeedTime, [Validators.min(0)]],
      maxSeedTime: [category.maxSeedTime],
    }, { validators: this.validateCategory });
  }
  
  /**
   * Custom validator for the "both disabled" rule in categories
   */
  private validateCategory(group: FormGroup): ValidationErrors | null {
    const maxRatio = group.get('maxRatio')?.value;
    const maxSeedTime = group.get('maxSeedTime')?.value;

    if (maxRatio < 0 && maxSeedTime < 0) {
      return { bothDisabled: true };
    }

    return null;
  }
  
  /**
   * Custom validator for unlinked categories - requires categories when unlinked handling is enabled
   */
  private validateUnlinkedCategories(group: FormGroup): ValidationErrors | null {
    const unlinkedEnabled = group.get('unlinkedEnabled')?.value;
    const unlinkedCategories = group.get('unlinkedCategories')?.value;

    if (unlinkedEnabled && (!unlinkedCategories || unlinkedCategories.length === 0)) {
      return { unlinkedCategoriesRequired: true };
    }

    return null;
  }
  
  /**
   * Helper method to get a category control as FormGroup for the template
   */
  getCategoryAsFormGroup(index: number): FormGroup {
    return this.categoriesFormArray.at(index) as FormGroup;
  }
  
  /**
   * Remove a category from the form array
   */
  removeCategory(index: number): void {
    this.categoriesFormArray.removeAt(index);
    this.downloadCleanerForm.markAsDirty();
  }

  /**
   * Update the form with values from the configuration
   */
  private updateForm(config: DownloadCleanerConfig): void {
    // Reset any existing categories
    this.categoriesFormArray.clear();

    // Add categories from config with validation
    if (config.categories && config.categories.length > 0) {
      config.categories.forEach(category => {
        this.addCategory(category);
      });
    }

    // Use the backend configuration directly without auto-switching to advanced mode
    const useAdvanced = config.useAdvancedScheduling || false;
    let jobSchedule = config.jobSchedule || { every: 1, type: ScheduleUnit.Hours };
    
    // If not using advanced scheduling, try to parse the cron expression to basic schedule
    if (!useAdvanced && config.cronExpression) {
      const parsedSchedule = this.downloadCleanerStore.parseCronExpression(config.cronExpression);
      if (parsedSchedule) {
        jobSchedule = {
          every: parsedSchedule.every,
          type: parsedSchedule.type as ScheduleUnit
        };
      }
      // Note: If parsing fails, we keep basic mode as requested by backend
    }

    // Update form values
    this.downloadCleanerForm.patchValue({
      enabled: config.enabled,
      useAdvancedScheduling: useAdvanced,
      cronExpression: config.cronExpression,
      deletePrivate: config.deletePrivate,
      unlinkedEnabled: config.unlinkedEnabled,
      unlinkedTargetCategory: config.unlinkedTargetCategory,
      unlinkedUseTag: config.unlinkedUseTag,
      unlinkedIgnoredRootDir: config.unlinkedIgnoredRootDir,
      unlinkedCategories: config.unlinkedCategories || []
    });

    // Update job schedule
    this.downloadCleanerForm.get('jobSchedule')?.patchValue({
      every: jobSchedule.every,
      type: jobSchedule.type
    });
    
    // Update form control states based on the configuration
    this.updateFormControlDisabledStates(config);
    
    // Store original values for change detection
    this.storeOriginalValues();
    
    // Track the enabled state for confirmation dialog logic
    this.previousEnabledState = config.enabled;
    
    // Mark form as initialized to enable confirmation dialogs for user actions
    this.formInitialized = true;
    
    // Mark form as pristine after loading
    this.downloadCleanerForm.markAsPristine();
  }

  /**
   * Clean up subscriptions when component is destroyed
   */
  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Set up listeners for form control value changes to manage dependent control states
   */
  private setupFormValueChangeListeners(): void {
    // Listen for changes to the 'enabled' control
    const enabledControl = this.downloadCleanerForm.get('enabled');
    if (enabledControl) {
      enabledControl.valueChanges
        .pipe(takeUntil(this.destroy$))
        .subscribe(enabled => {
          // Only show confirmation dialog if form is initialized and user is trying to enable
          if (this.formInitialized && enabled && !this.previousEnabledState) {
            this.showEnableConfirmationDialog();
          } else {
            // Update control states normally
            this.updateMainControlsState(enabled);
            this.previousEnabledState = enabled;
          }
        });
    }

    // Listen for changes to the 'useAdvancedScheduling' control
    const advancedControl = this.downloadCleanerForm.get('useAdvancedScheduling');
    if (advancedControl) {
      advancedControl.valueChanges
        .pipe(takeUntil(this.destroy$))
        .subscribe(useAdvanced => {
          const enabled = this.downloadCleanerForm.get('enabled')?.value || false;
          if (enabled) {
            const cronExpressionControl = this.downloadCleanerForm.get('cronExpression');
            const jobScheduleGroup = this.downloadCleanerForm.get('jobSchedule') as FormGroup;
            const everyControl = jobScheduleGroup?.get('every');
            const typeControl = jobScheduleGroup?.get('type');
            
            if (useAdvanced) {
              if (cronExpressionControl) cronExpressionControl.enable();
              if (everyControl) everyControl.disable();
              if (typeControl) typeControl.disable();
            } else {
              if (cronExpressionControl) cronExpressionControl.disable();
              if (everyControl) everyControl.enable();
              if (typeControl) typeControl.enable();
            }
          }
        });
    }

    // Listen for changes to the 'unlinkedEnabled' control
    const unlinkedEnabledControl = this.downloadCleanerForm.get('unlinkedEnabled');
    if (unlinkedEnabledControl) {
      unlinkedEnabledControl.valueChanges
        .pipe(takeUntil(this.destroy$))
        .subscribe(enabled => {
          this.updateUnlinkedControlsState(enabled);
        });
    }

    // Listen for changes to the schedule type to ensure dropdown isn't empty
    const scheduleTypeControl = this.downloadCleanerForm.get('jobSchedule.type');
    if (scheduleTypeControl) {
      scheduleTypeControl.valueChanges
        .pipe(takeUntil(this.destroy$))
        .subscribe(() => {
          // Ensure the selected value is valid for the new type
          const everyControl = this.downloadCleanerForm.get('jobSchedule.every');
          const currentValue = everyControl?.value;
          const scheduleType = this.downloadCleanerForm.get('jobSchedule.type')?.value;
          
          const validValues = ScheduleOptions[scheduleType as keyof typeof ScheduleOptions];
          if (validValues && currentValue && !validValues.includes(currentValue)) {
            everyControl?.setValue(validValues[0]);
          }
        });
    }
    
    // Listen to all form changes to check for actual differences from original values
    this.downloadCleanerForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.hasActualChanges = this.formValuesChanged();
      });
  }

  /**
   * Store original form values for dirty checking
   */
  private storeOriginalValues(): void {
    // Create a deep copy of the form values to ensure proper comparison
    // Using getRawValue() instead of just value to include disabled controls
    this.originalFormValues = JSON.parse(JSON.stringify(this.downloadCleanerForm.getRawValue()));
    this.hasActualChanges = false;
  }

  /**
   * Check if the current form values are different from the original values
   */
  formValuesChanged(): boolean {
    if (!this.originalFormValues) return false;
    
    // Use getRawValue() to include disabled controls in the comparison
    const currentValues = this.downloadCleanerForm.getRawValue();
    return !this.isEqual(currentValues, this.originalFormValues);
  }

  /**
   * Deep compare two objects for equality
   */
  private isEqual(obj1: any, obj2: any): boolean {
    if (obj1 === obj2) return true;
    if (obj1 === null || obj2 === null) return false;
    if (typeof obj1 !== 'object' || typeof obj2 !== 'object') return obj1 === obj2;

    const keys1 = Object.keys(obj1);
    const keys2 = Object.keys(obj2);

    if (keys1.length !== keys2.length) return false;

    for (const key of keys1) {
      if (!keys2.includes(key)) return false;
      if (!this.isEqual(obj1[key], obj2[key])) return false;
    }

    return true;
  }

  /**
   * Update form control disabled states based on the configuration
   */
  private updateFormControlDisabledStates(config: DownloadCleanerConfig): void {
    // Update main controls based on enabled state
    this.updateMainControlsState(config.enabled);
    
    // Update schedule controls based on advanced scheduling
    const cronControl = this.downloadCleanerForm.get('cronExpression');
    const jobScheduleControl = this.downloadCleanerForm.get('jobSchedule');

    if (config.useAdvancedScheduling) {
      jobScheduleControl?.disable({ emitEvent: false });
      cronControl?.enable({ emitEvent: false });
    } else {
      cronControl?.disable({ emitEvent: false });
      jobScheduleControl?.enable({ emitEvent: false });
    }
  }
  
  /**
   * Update the state of main controls based on the 'enabled' control value
   */
  private updateMainControlsState(enabled: boolean): void {
    const useAdvancedScheduling = this.downloadCleanerForm.get('useAdvancedScheduling')?.value || false;
    const cronExpressionControl = this.downloadCleanerForm.get('cronExpression');
    const jobScheduleGroup = this.downloadCleanerForm.get('jobSchedule') as FormGroup;
    const everyControl = jobScheduleGroup?.get('every');
    const typeControl = jobScheduleGroup?.get('type');

    if (enabled) {
      // Enable scheduling controls based on mode
      if (useAdvancedScheduling) {
        cronExpressionControl?.enable();
        everyControl?.disable();
        typeControl?.disable();
      } else {
        cronExpressionControl?.disable();
        everyControl?.enable();
        typeControl?.enable();
      }
      
      // Update individual config sections only if they are enabled
      const categoriesControl = this.categoriesFormArray;
      const deletePrivateControl = this.downloadCleanerForm.get('deletePrivate');
      const unlinkedEnabledControl = this.downloadCleanerForm.get('unlinkedEnabled');
      const useAdvancedSchedulingControl = this.downloadCleanerForm.get('useAdvancedScheduling');
      
      categoriesControl?.enable();
      deletePrivateControl?.enable();
      unlinkedEnabledControl?.enable();
      useAdvancedSchedulingControl?.enable();
      
      // Update unlinked controls based on unlinkedEnabled value
      const unlinkedEnabled = unlinkedEnabledControl?.value;
      this.updateUnlinkedControlsState(unlinkedEnabled);
    } else {
      // Disable all scheduling controls
      cronExpressionControl?.disable();
      everyControl?.disable();
      typeControl?.disable();
      
      // Disable all other controls when the feature is disabled
      const categoriesControl = this.categoriesFormArray;
      const deletePrivateControl = this.downloadCleanerForm.get('deletePrivate');
      const unlinkedEnabledControl = this.downloadCleanerForm.get('unlinkedEnabled');
      const useAdvancedSchedulingControl = this.downloadCleanerForm.get('useAdvancedScheduling');
      
      categoriesControl?.disable();
      deletePrivateControl?.disable();
      unlinkedEnabledControl?.disable();
      useAdvancedSchedulingControl?.disable();
      
      // Always disable unlinked controls when main feature is disabled
      this.updateUnlinkedControlsState(false);
      
      // Save current active accordion state before clearing it
      // This will be empty when we collapse all accordions
      this.activeAccordionIndices = [];
    }
  }

  /**
   * Save the download cleaner configuration
   */
  saveDownloadCleanerConfig(): void {
    // Mark all form controls as touched to trigger validation
    this.markFormGroupTouched(this.downloadCleanerForm);

    if (this.downloadCleanerForm.valid) {
      // Get form values including disabled controls
      const formValues = this.downloadCleanerForm.getRawValue();

      // Create config object from form values
      const config: DownloadCleanerConfig = {
        enabled: formValues.enabled,
        useAdvancedScheduling: formValues.useAdvancedScheduling,
        cronExpression: formValues.useAdvancedScheduling ? 
          formValues.cronExpression : 
          // If in basic mode, generate cron expression from the schedule
          this.downloadCleanerStore.generateCronExpression(formValues.jobSchedule),
        jobSchedule: formValues.jobSchedule,
        categories: formValues.categories,
        deletePrivate: formValues.deletePrivate,
        unlinkedEnabled: formValues.unlinkedEnabled,
        unlinkedTargetCategory: formValues.unlinkedTargetCategory,
        unlinkedUseTag: formValues.unlinkedUseTag,
        unlinkedIgnoredRootDir: formValues.unlinkedIgnoredRootDir,
        unlinkedCategories: formValues.unlinkedCategories || []
      };

      // Save the configuration using the new store API
      this.downloadCleanerStore.saveDownloadCleanerConfig(config);
      
      // Setup a one-time check to mark form as pristine after successful save
      const checkSaveCompletion = () => {
        const saving = this.downloadCleanerSaving();
        const error = this.downloadCleanerError();
        
        if (!saving && !error) {
          // Mark form as pristine after successful save
          this.downloadCleanerForm.markAsPristine();
          // Update original values reference
          this.storeOriginalValues();
          // Emit saved event 
          this.saved.emit();
          // Display success message
          this.notificationService.showSuccess('Download cleaner configuration saved successfully');
        } else if (!saving && error) {
          // If there's an error, we can stop checking
          // No need to show error toast here, it's handled by the LoadingErrorStateComponent
        } else {
          // If still saving, check again in a moment
          setTimeout(checkSaveCompletion, 100);
        }
      };
      
      // Start checking for save completion
      checkSaveCompletion();
    } else {
      this.notificationService.showValidationError();
    }
  }

  /**
   * Reset the download cleaner configuration form to default values
   */
  resetDownloadCleanerConfig(): void {
    // Clear categories
    this.categoriesFormArray.clear();
    
    // Reset form to default values
    this.downloadCleanerForm.reset({
      enabled: false,
      useAdvancedScheduling: false,
      cronExpression: '0 0 * * * ?',
      jobSchedule: {
        type: ScheduleUnit.Minutes,
        every: 5
      },
      categories: [],
      deletePrivate: false,
      unlinkedEnabled: false,
      unlinkedTargetCategory: 'cleanuparr-unlinked',
      unlinkedUseTag: false,
      unlinkedIgnoredRootDir: '',
      unlinkedCategories: []
    });

    // Reset accordion indices
    this.activeAccordionIndices = [];

    // Manually update control states after reset
    this.updateMainControlsState(false);
    
    // Mark form as dirty so the save button is enabled after reset
    this.downloadCleanerForm.markAsDirty();
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
   * Check if a form control has an error after it's been touched
   */
  hasError(controlName: string, errorName: string): boolean {
    const control = this.downloadCleanerForm.get(controlName);
    return control ? control.touched && control.hasError(errorName) : false;
  }
  
  /**
   * Check if the form has the unlinked categories validation error
   */
  hasUnlinkedCategoriesError(): boolean {
    return this.downloadCleanerForm.touched && this.downloadCleanerForm.hasError('unlinkedCategoriesRequired');
  }
  
  /**
   * Get schedule value options based on the current schedule unit type
   */
  getScheduleValueOptions(): {label: string, value: number}[] {
    const scheduleType = this.downloadCleanerForm.get('jobSchedule.type')?.value as ScheduleUnit;
    if (scheduleType === ScheduleUnit.Seconds) {
      return this.scheduleValueOptions[ScheduleUnit.Seconds];
    } else if (scheduleType === ScheduleUnit.Minutes) {
      return this.scheduleValueOptions[ScheduleUnit.Minutes];
    } else if (scheduleType === ScheduleUnit.Hours) {
      return this.scheduleValueOptions[ScheduleUnit.Hours];
    }
    return this.scheduleValueOptions[ScheduleUnit.Minutes]; // Default to minutes
  }

  /**
   * Get nested form control errors
   */
  hasNestedError(parentName: string, controlName: string, errorName: string): boolean {
    const parentControl = this.downloadCleanerForm.get(parentName);
    if (!parentControl || !(parentControl instanceof FormGroup)) {
      return false;
    }

    const control = parentControl.get(controlName);
    return control ? control.touched && control.hasError(errorName) : false;
  }

  /**
   * Check if a control in a form array has an error
   */
  hasCategoryError(index: number, controlName: string, errorName: string): boolean {
    const categoryGroup = this.categoriesFormArray.at(index) as FormGroup;
    if (!categoryGroup) return false;
    
    const control = categoryGroup.get(controlName);
    return control ? control.touched && control.hasError(errorName) : false;
  }

  /**
   * Check if a category form control has an error
   */
  hasCategoryControlError(categoryIndex: number, controlName: string, errorName: string): boolean {
    const categoryGroup = this.categoriesFormArray.at(categoryIndex);
    const control = categoryGroup.get(controlName);
    return control ? control.touched && control.hasError(errorName) : false;
  }
  
  /**
   * Check if a category form group itself has an error (not tied to a specific control)
   */
  hasCategoryGroupError(categoryIndex: number, errorName: string): boolean {
    const categoryGroup = this.categoriesFormArray.at(categoryIndex);
    return categoryGroup ? categoryGroup.touched && categoryGroup.hasError(errorName) : false;
  }

  /**
   * Update the state of unlinked controls based on whether unlinked handling is enabled
   */
  private updateUnlinkedControlsState(enabled: boolean): void {
    const targetCategoryControl = this.downloadCleanerForm.get('unlinkedTargetCategory');
    const useTagControl = this.downloadCleanerForm.get('unlinkedUseTag');
    const ignoredRootDirControl = this.downloadCleanerForm.get('unlinkedIgnoredRootDir');
    const categoriesControl = this.downloadCleanerForm.get('unlinkedCategories');
    
    // Disable emitting events during bulk changes
    const options = { emitEvent: false };
    
    if (enabled) {
      // Enable all unlinked controls
      targetCategoryControl?.enable(options);
      useTagControl?.enable(options);
      ignoredRootDirControl?.enable(options);
      categoriesControl?.enable(options);
    } else {
      // Disable all unlinked controls
      targetCategoryControl?.disable(options);
      useTagControl?.disable(options);
      ignoredRootDirControl?.disable(options);
      categoriesControl?.disable(options);
    }
  }

  /**
   * Simple test method to check unlinkedCategories functionality
   * Call from browser console: ng.getComponent(document.querySelector('app-download-cleaner-settings')).testUnlinkedCategories()
   */
  testUnlinkedCategories(): void {
    console.log('=== TESTING UNLINKED CATEGORIES ===');
    
    const control = this.downloadCleanerForm.get('unlinkedCategories');
    console.log('Current value:', control?.value);
    console.log('Control disabled:', control?.disabled);
    console.log('Control status:', control?.status);
    
    // Test setting values
    console.log('Setting test values: ["movies", "tv-shows"]');
    control?.setValue(['movies', 'tv-shows']);
    
    console.log('Value after setting:', control?.value);
    
    // Test what getRawValue returns
    const rawValues = this.downloadCleanerForm.getRawValue();
    console.log('getRawValue().unlinkedCategories:', rawValues.unlinkedCategories);
    
    console.log('=== END TEST ===');
  }

  /**
   * Minimal complete method for autocomplete - just returns empty array to allow manual input
   */
  onUnlinkedCategoriesComplete(event: any): void {
    // Return empty array - this allows users to type any value manually
    // PrimeNG requires this method even when we don't want suggestions
    this.unlinkedCategoriesSuggestions = [];
  }

  /**
   * Show confirmation dialog when enabling the download cleaner
   */
  private showEnableConfirmationDialog(): void {
    this.confirmationService.confirm({
      header: 'Enable Download Cleaner',
      message: 'To avoid affecting items that are awaiting to be imported, please ensure that your Sonarr, Radarr, and Lidarr instances have been properly configured prior to enabling the Download Cleaner.<br/><br/>Are you sure you want to proceed?',
      icon: 'pi pi-exclamation-triangle',
      acceptIcon: 'pi pi-check',
      rejectIcon: 'pi pi-times',
      acceptLabel: 'Yes, Enable',
      rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-warning',
      accept: () => {
        // User confirmed, update control states and track state
        this.updateMainControlsState(true);
        this.previousEnabledState = true;
      },
      reject: () => {
        // User cancelled, revert the checkbox without triggering value change
        const enabledControl = this.downloadCleanerForm.get('enabled');
        if (enabledControl) {
          enabledControl.setValue(false, { emitEvent: false });
          this.previousEnabledState = false;
        }
      }
    });
  }

  // Add any other necessary methods here
}
