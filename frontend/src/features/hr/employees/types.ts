/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.3 — HR_EMPLOYEES. */

export type EmployeeStatus = 'Draft' | 'Active' | 'OnLeave' | 'Suspended' | 'Terminated';
export type EmploymentType = 'FullTime' | 'PartTime' | 'Temporary';
export type Gender = 'Male' | 'Female';
export type MaritalStatus = 'Single' | 'Married' | 'Divorced' | 'Widowed';
export type ContractType = 'FixedTerm' | 'Indefinite';
export type EmploymentContractStatus = 'Draft' | 'Active' | 'Expired' | 'Terminated';
export type ContractLineType = 'Earning' | 'Deduction';

export interface EmployeeListItem {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  branchId: number | null;
  orgUnitId: number;
  jobPositionId: number;
  jobGradeId: number;
  managerId: number | null;
  userId: number | null;
  hireDate: string;
  employmentType: EmploymentType;
  status: EmployeeStatus;
}

export interface EmployeePersonalData {
  id: number;
  nationalIdLast4: string;
  bankIbanLast4: string | null;
  bankName: string | null;
  bankId: number | null;
  nationalityId: number | null;
  cityId: number | null;
  militaryStatusId: number | null;
  qualificationTypeId: number | null;
  birthDate: string | null;
  gender: Gender | null;
  maritalStatus: MaritalStatus | null;
  address: string | null;
  phoneNumber: string | null;
  personalEmail: string | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
  emergencyContactRelationshipTypeId: number | null;
}

export interface EmployeeDetail extends EmployeeListItem {
  costCenterDimensionValueId: number | null;
  terminationDate: string | null;
  personalData: EmployeePersonalData | null;
}

export type EmployeeInput = {
  code?: string;
  nameAr: string;
  nameEn: string;
  branchId: number;
  orgUnitId: number;
  jobPositionId: number;
  jobGradeId: number;
  managerId?: number;
  userId?: number;
  hireDate: string;
  employmentType: EmploymentType;
  costCenterDimensionValueId?: number;
};

export type EmployeeUpdateInput = Omit<EmployeeInput, 'code'> & { isActive: boolean };

export type PersonalDataInput = {
  nationalId: string;
  bankIban?: string;
  bankName?: string;
  bankId?: number;
  nationalityId?: number;
  cityId?: number;
  militaryStatusId?: number;
  qualificationTypeId?: number;
  birthDate: string;
  gender: Gender;
  maritalStatus: MaritalStatus;
  address?: string;
  phoneNumber?: string;
  personalEmail?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  emergencyContactRelationshipTypeId?: number;
};

export interface EmployeeLookupItem {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
}

export interface ContractLine {
  id: number;
  nameAr: string;
  nameEn: string;
  amount: number;
  type: ContractLineType;
  isTaxable: boolean;
  isInsurable: boolean;
  order: number;
}

export type ContractLineInput = {
  nameAr: string;
  nameEn: string;
  amount: number;
  type: ContractLineType;
  isTaxable: boolean;
  isInsurable: boolean;
  order: number;
};

export interface EmploymentContract {
  id: number;
  employeeId: number;
  branchId: number | null;
  contractType: ContractType;
  startDate: string;
  endDate: string | null;
  probationEndDate: string | null;
  basicSalary: number;
  insurableWage: number;
  workingHoursPerDay: number;
  status: EmploymentContractStatus;
  previousContractId: number | null;
  approvalInstanceId: number | null;
  attachmentId: number | null;
  /** Populated by GetById; empty on the paginated list (Docs/Implementation/Phase-3C-Research.md §3.1). */
  lines: ContractLine[];
}

export type EmploymentContractInput = {
  contractType: ContractType;
  startDate: string;
  endDate?: string;
  probationEndDate?: string;
  basicSalary: number;
  insurableWage: number;
  workingHoursPerDay: number;
  lines?: ContractLineInput[];
};

export interface EmployeeDocument {
  id: number;
  employeeId: number;
  branchId: number | null;
  employeeDocumentTypeId: number;
  issueDate: string;
  expiryDate: string | null;
  attachmentId: number;
  documentNumber: string | null;
}

export type EmployeeDocumentInput = {
  employeeDocumentTypeId: number;
  issueDate: string;
  expiryDate?: string;
  attachmentId: number;
  documentNumber?: string;
};

export interface EmployeeCertification {
  id: number;
  employeeId: number;
  branchId: number | null;
  nameAr: string;
  nameEn: string;
  issuer: string;
  issueDate: string;
  expiryDate: string | null;
  certificateNumber: string | null;
  attachmentId: number | null;
}

export type EmployeeCertificationInput = {
  nameAr: string;
  nameEn: string;
  issuer: string;
  issueDate: string;
  expiryDate?: string;
  certificateNumber?: string;
};
