import axios, { AxiosError, AxiosRequestConfig, AxiosResponse, isAxiosError } from "axios";
import {
  AuditReport,
  AuditReportChecklistStatus,
  AuditReportChecklistUploadDTO,
  AuditReportEditDTO,
  AuditReportStatus
} from "../models/AuditReport";
import {
  AuditAgenda,
  AuditAuditor,
  AuditInternalSearch,
  AuditSchedule,
  AuditScheduleBuyerAuditEditDTO,
  AuditScheduleInternalDTO,
  AuditScheduleInternalEditDTO,
  AuditScheduleInternals,
  AuditScheduleMiscFileDTO,
  AuditScheduleSupplierDTO,
  AuditScheduleSupplierEditDTO,
  AuditScheduleSuppliers,
  AuditSupplierSearch,
  AuditType
} from "../models/AuditSchedule";
import {
  ApplicationType,
  ApplicationScheme,
  ApplicationStatus,
  IApplicationSearchForm,
  ApplicationDetail
} from "../models/Application";
import {
  HalalFileDetail,
  ISearchHalalFile,
  HalalFileCoverLetter,
  HalalFileAccessSubmit,
  HalalFileStatus,
  HalalFileAccessComment,
  HalalFileSequence,
  ISearchHalalFileLogRequest
} from "../models/HalalFile";
import { BusinessType } from "../models/BusinessType";
import { CertificateDTO, CertificateType, CertificationBody } from "../models/Certificate";
import {
  CertificateRequestDTO,
  CertificateRequestRemarkDTO,
  CertificateRequestStatus,
  ICertificateRequestAdminConfim,
  ICertificateRequestAdminReject,
  ICertificateRequestSupplierAccept,
  ICertificateRequestSupplierReject,
  ICertificateRequestSupplierSearchForm,
  ICreateCertificateRequest
} from "../models/CertificateRequest";
import { ChecklistResponseDTO, ChecklistResponseEditDTO } from "../models/ChecklistResponse";
import {
  ChecklistTemplateDTO,
  ChecklistTemplateEditDTO,
  ChecklistTemplateSection,
  ChecklistTemplateStatus
} from "../models/ChecklistTemplate";
import { ContactPerson } from "../models/ContactPerson";
import { DevicePlatform } from "../models/DevicePlatform";
import {
  DocumentComplianceFileDTO,
  DocumentFolderDTO,
  DocumentFolderDTOMonitoring,
  DocumentMonitoringFileDTO,
  DocumentType,
  IDocumentFolder,
  IDocumentFolderMonitoring,
  IDocumentMonitoringApiData,
  IDocumentSearchForm
} from "../models/Document";
import { Designation, IDesignationSearchForm } from "../models/HumanResource/Designation";
import {
  Employee,
  EmployeeGroup,
  EmployeeStatus,
  IEmployeeGroupSearchForm,
  IEmployeeSearchForm
} from "../models/HumanResource/Employee";
import {
  EmployeeMedicalCheckup,
  EmployeeMedicalCheckupDetail,
  IEmployeeMedicalCheckupSearchForm,
  MedicalCheckupType
} from "../models/HumanResource/MedicalCheckup";
import {
  EmployeeTraining,
  EmployeeTrainingDetail,
  IEmployeeTrainingSearchForm,
  ITrainingSearchForm,
  TrainingBase,
  TrainingDetail
} from "../models/HumanResource/Training";
import {
  ITrainingCourseSearchForm,
  TrainingCourseBase,
  TrainingCourseDetail,
  TrainingCourseStatus,
  CourseType
} from "../models/HumanResource/TrainingCourse";
import { CourseAssignment } from "../models/HumanResource/CourseAssignment";
import {
  ITrainingProviderSearchForm,
  TrainingProvider
} from "../models/HumanResource/TrainingProvider";
import { BaseTrainingType } from "../models/HumanResource/TrainingType";
import {
  EmployeeVaccinationBase,
  EmployeeVaccinationDetail,
  IEmployeeVaccinationSearchForm,
  VaccinationType
} from "../models/HumanResource/Vaccination";
import { LogisticProviderDTO } from "../models/Logistic";
import { ManufacturerDTO } from "../models/Manufacturer";
import { MenuDTO } from "../models/Menu";
import {
  NonConformance,
  NonConformanceEditDTO,
  NonConformanceEditDTOV2,
  NonConformanceGrading,
  NonConformanceStatus,
  NonConformanceType
} from "../models/NonConformance";
import { IOrganizationSearchForm, Organization, OrganizationEditDTO } from "../models/Organization";
import {
  ProductBase,
  ProductBrand,
  ProductGroup,
  ProductStatus,
  ProductStatusUpdate,
  SearchProductMaster
} from "../models/Product";
import { RawMaterialPurchaseBase, RawMaterialPurchaseDetail } from "../models/RawMaterialPurchase";
import {
  IRawMaterialSearchForm,
  ISearchAssociatedProduct,
  RawMaterialBase,
  RawMaterialEditDTO,
  RawMaterialOrigin,
  RawMaterialStatus,
  RawMaterialStatusUpdate,
  RawMaterialGroup
} from "../models/RawMaterials";
import { Role } from "../models/Role";
import { ISiteSearchForm, Site, SiteEditDTO } from "../models/Site";
import { SiteType } from "../models/SiteType";
import { Supplier, SupplierCategory } from "../models/Supplier";
import {
  IResetPasswordBase,
  ISearchUserLogRequest,
  IUserSearchForm,
  UserBase,
  UserDTO
} from "../models/User";
import { UserConfirmationDTO, UserConfirmationRequestDTO } from "../models/UserConfirmation";
import { TOKEN_KEY } from "../utils/constants";
import { logout } from "./auth";
import { getConfig } from "./config";

import { CreateAuditConductedBody, SearchAuditConductedBody } from "@/models/Audit/Conducted";
import { SearchEmailHistoryDTO } from "@/models/EmailHistory/interface";
import { MedicalCheckupTypeSearchForm } from "@/models/HumanResource/MedicalCheckupType";
import { NotificationListRequestDTO } from "@/models/Monitoring/TemperatureHumidity/NotificationList/interface";
import { SensorListRequestDTO } from "@/models/Monitoring/TemperatureHumidity/SensorList/interface";
import { ISearchGateWay } from "@/models/Monitoring/TemperatureHumidity/interface";
import { INotificationSettingDetail } from "@/models/NotificationSetting/interface";
import { NotificationTemplate } from "@/models/NotificationTemplate";
import { SearchNotificationTemplateDTO } from "@/models/NotificationTemplate/interface";
import { IIncomingCertReq } from "@/models/Operation/IncomingCertificateRequest/interface";
import { IOutgoingCertReq } from "@/models/Operation/OutgoingCertificateRequest/interface";
import { IRawMaterialPurchase } from "@/models/Purchasing/RawMaterialPurchase/interface";
import { CustomSuccessResponse } from "@/models/Request";
import { ISearchGlobalPayload } from "@/models/SearchGlobal/interface";
import {
  IAdminGatewayPayload,
  IDeleteMappingDeviceToOrgId,
  IDeviceNotification,
  IGatewayPayload,
  IMappingDeviceToOrgId,
  ISearchDeviceHistory,
  IUpdateDevice
} from "@/models/TemperatureHumidity";
import {
  ICountrySearchForm,
  IStateSearchForm,
  ICountryDetail,
  ICountryDeletePayload,
  IStateCreatePayload,
  IStateDeletePayload,
  IStateUpdatePayload
} from "@/models/common/Country";
import { SearchInternalAuditReportDTO } from "../models/Audit/Report/internal/interface";
import { SearchSupplierAuditReportDTO } from "../models/Audit/Report/supplier/interface";
import {
  SearchCertificationBodyAuditScheduleDTO,
  SearchThirdPartyAuditScheduleDTO,
  SearchSupplierAuditScheduleDTO
} from "../models/Audit/Schedule/supplier/interface";
import {
  ISearchAssociatedCertificateProductPayload,
  ISearchCertificateBody,
  ISearchProductCertsBody
} from "../models/Certificate/CertificateBody/interface";
import { SearchChecklistRequestDTO } from "../models/ChecklistTemplate/interface";
import {
  IComplaintCloseDTO,
  IComplaintResolveDTO,
  IComplaintSendDTO,
  INonConformanceReviewDTO,
  ISearchComplaint,
  ISearchNonConformance,
  ISearchSubmissionHistory
} from "../models/Complaint/interface";
import { Department, IDepartmentSearchForm } from "../models/HumanResource/Department";
import { ITyphoidVaccinationEditDTO } from "../models/HumanResource/TyphoidVaccination/interface";
import { SearchManufacturer } from "../models/Manufacturer/interface";
import { NonConformanceTypeRequestDTO } from "../models/NonConformance/interface";
import { SearchNotificationDTO } from "../models/Notification/interface";
import {
  IRawMaterialCertMgmt,
  ISearchAssociatedRawMaterialPayload
} from "../models/Operation/RawMaterialCertificateMgmt/interface";
import {
  RawMaterialMasterListRequestDTO,
  RawMaterialMasterListRequestDTOV2
} from "../models/Operation/RawMaterialMasterList/interface";
import {
  SearchProductBodyDTO,
  SearchProductBrandBodyDTO,
  SearchProductGroupBodyDTO
} from "../models/Product/interface";
import { SearchSiteRequestDTO } from "../models/Site/interface";
import { SearchSupplier } from "../models/Supplier/interface";
import { WidgetData } from "../models/Widgets/interface";
import CONST from "../utils/services/api.constants";
import * as Api from "../utils/services/api.request";
import { SearchCertificationBodyAuditReportDTO } from "../models/Audit/Report/certificationBody/interface";
import { IAdminSettings } from "@/models/AdminSettings";
import type { OrganizationAutoCertificateRequestUpdatePayload } from "@/models/AdminSettings/AutoCertificateRequestSetting";
import { IRoleAccessSearchForm, IRoleAccessUpdateDTO } from "@/models/RoleAccess";

const config = await getConfig();
const baseURL = config.baseURL;
/*
let baseURL = import.meta.env.BASE_URL;

const envMode = import.meta.env.MODE;
if (envMode.toLowerCase() === "development") {
  baseURL = "http://localhost:5276";
} else if (envMode.toLowerCase() === "staging") {
  baseURL = "https://hias-net-core-dev.azurewebsites.net";
} else if (envMode.toLowerCase() === "production") {
  baseURL = "https://hias-net-core-dev.azurewebsites.net";
}
*/

const axiosClient = axios.create({
  baseURL: `${baseURL}/api`,
  withCredentials: true
});

axiosClient.interceptors.response.use(
  function (response: AxiosResponse) {
    return response;
  },

  function (error: AxiosError) {
    const responseUrl = error.request.responseURL ?? "";
    if (error.request.status === 404 && responseUrl.includes("/login")) {
      logout();
    }

    return Promise.reject(error);
  }
);

const addHeader = (data: AxiosRequestConfig) => {
  return {
    ...data,
    headers: {
      ...data.headers,
      Authorization: localStorage.getItem(TOKEN_KEY) || ""
    }
  };
};

const api = {
  organizationRole: {
    delete: (roleId: number, orgId: number) => {
      return Api.deleteData({
        url: CONST.organizationRole.delete(roleId, orgId)
      });
    },
    getAll: () => {
      return Api.get({
        url: CONST.organizationRole.getAll
      });
    },
    search: (payload: IRoleAccessSearchForm) => {
      return Api.post({
        url: CONST.organizationRole.search,
        data: payload
      });
    },
    getById: (id: number) => {
      return Api.get({
        url: CONST.organizationRole.getById(id)
      });
    },
    create: (payload: any) => {
      return Api.post({
        url: CONST.organizationRole.create,
        data: payload
      });
    },
    update: (payload: IRoleAccessUpdateDTO) => {
      return Api.post({
        url: CONST.organizationRole.update,
        data: payload
      });
    }
  },
  auth: {
    login: (email: string, password: string) => {
      return axiosClient<UserDTO>({
        method: "POST",
        url: "/auth/login",
        data: {
          email,
          password
        }
      });
    },
    logout: () => {
      return Api.get({
        url: CONST.auth.logout
      });
    },
    hasPermission: (permission: string) => {
      return axiosClient<boolean>(
        addHeader({
          method: "GET",
          url: `/auth/has-permission/${permission}`
        })
      );
    },
    isDH: () => {
      return axiosClient<boolean>(
        addHeader({
          method: "GET",
          url: "/auth/is-dh"
        })
      );
    },
    deleteRequest: (email: string, password: string) => {
      return Api.post({
        url: CONST.user.deleteRequest,
        data: {
          email,
          password
        }
      });
    }
  },
  resetPassword: {
    verify: (email: string) => {
      // TODO: typebind me
      return axiosClient(
        addHeader({
          method: "POST",
          url: `/reset-password/verify/${email}`
        })
      );
    },
    verifyV2: (email: string) => {
      return Api.post({
        url: CONST.resetPassword.verify(email)
      });
    },
    verifyToken: (token: string) => {
      return axiosClient<boolean>(
        addHeader({
          method: "GET",
          url: `/reset-password/${token}`
        })
      );
    },
    submit: (token: string, newPassword: string) => {
      return axiosClient<boolean>(
        addHeader({
          method: "POST",
          url: `/reset-password/submit/${token}/${newPassword}`
        })
      );
    }
  },
  documentFolder: {
    getAll: (folderType: 0 | 1) => {
      return axiosClient<DocumentFolderDTO[]>(
        addHeader({
          method: "GET",
          url: `/document-folder/all/${folderType}`
        })
      );
    },
    getById: (id: number, folderType: 0 | 1) => {
      return axiosClient<DocumentFolderDTOMonitoring | DocumentFolderDTO>(
        addHeader({
          method: "GET",
          url: `/document-folder/${id}?documentFolderType=${folderType}`
        })
      );
    },
    create: (documentFolder: DocumentFolderDTO) => {
      return axiosClient<DocumentFolderDTO>(
        addHeader({
          method: "POST",
          url: "/document-folder/create",
          data: documentFolder
        })
      );
    },
    createDocumentFolder: (payload: IDocumentFolder, folderTypeName: string) => {
      return Api.post({
        url: CONST.documentFolder.createFolder(folderTypeName),
        data: payload
      });
    },
    updateDocumentFolder: (
      payload: IDocumentFolder | IDocumentFolderMonitoring,
      folderTypeName: string
    ) => {
      return Api.post({
        url: CONST.documentFolder.updateFolder(folderTypeName),
        data: payload
      });
    },
    update: (documentFolder: DocumentFolderDTO) => {
      return axiosClient<DocumentFolderDTO>(
        addHeader({
          method: "POST",
          url: "/document-folder/update",
          data: documentFolder
        })
      );
    },
    updateByFolderId: (documentFolder: DocumentFolderDTOMonitoring) => {
      return axiosClient<DocumentFolderDTOMonitoring>(
        addHeader({
          method: "POST",
          url: "/v2/api/document-folder/monitoring/update",
          data: documentFolder
        })
      );
    },
    delete: (documentFolder: DocumentFolderDTO) => {
      return axiosClient<DocumentFolderDTO>(
        addHeader({
          method: "POST",
          url: "/document-folder/delete",
          data: documentFolder
        })
      );
    },
    deleteByFolderId: (payload: IDocumentFolder, folderType: string) => {
      return Api.post({
        url: CONST.documentFolder.deleteFolder(folderType),
        data: payload
      });
    }
  },
  documentComplianceFile: {
    searchAll: (payload: IDocumentSearchForm) => {
      return Api.post({
        url: CONST.documentFolder.compliance.searchAll,
        data: payload
      });
    },
    getAllByFolderIdV2: (id: number) => {
      return Api.get({
        url: CONST.documentFolder.compliance.documentComplianceFile(id),
        data: {}
      });
    },
    getAllByFolderIdUploadV2: (id: number) => {
      return Api.get({
        url: CONST.documentFolder.compliance.documentComplianceFileUploaded(id),
        data: {}
      });
    },
    getAllByFolderId: (id: number) => {
      return axiosClient<DocumentComplianceFileDTO[]>(
        addHeader({
          method: "GET",
          url: `/document-compliance-file/folder/${id}`
        })
      );
    },
    getByFolderId: (id: number, payload: IDocumentSearchForm) => {
      return Api.get({
        url: CONST.documentFolder.compliance.getByFolderId(id),
        data: payload
      });
    },
    getById: (id: number) => {
      return axiosClient<DocumentComplianceFileDTO>(
        addHeader({
          method: "GET",
          url: `/document-compliance-file/${id}`
        })
      );
    },
    createUploadFile: (payload: FormData) => {
      return Api.post({
        url: CONST.documentFolder.compliance.createUploadFile,
        data: payload
      });
    },
    create: (documentComplianceFile: DocumentComplianceFileDTO) => {
      return axiosClient<DocumentComplianceFileDTO>(
        addHeader({
          method: "POST",
          url: "/document-compliance-file/create",
          headers: { "Content-Type": "multipart/form-data" },
          data: documentComplianceFile
        })
      );
    },
    updateUploadFile: (payload: FormData) => {
      return Api.post({
        url: CONST.documentFolder.compliance.updateUploadFile,
        data: payload
      });
    },
    update: (documentComplianceFile: DocumentComplianceFileDTO) => {
      return axiosClient<DocumentComplianceFileDTO>(
        addHeader({
          method: "POST",
          url: "/document-compliance-file/update",
          headers: { "Content-Type": "multipart/form-data" },
          data: documentComplianceFile
        })
      );
    },
    deleteUploadFile: (payload: DocumentComplianceFileDTO) => {
      return Api.post({
        url: CONST.documentFolder.compliance.deleteUploadFile,
        data: payload
      });
    },
    delete: (documentComplianceFile: DocumentComplianceFileDTO) => {
      return axiosClient<DocumentComplianceFileDTO>(
        addHeader({
          method: "POST",
          url: "/document-compliance-file/delete",
          data: documentComplianceFile
        })
      );
    }
  },
  documentMonitoringFile: {
    searchAll: (payload: IDocumentSearchForm) => {
      return Api.post({
        url: CONST.documentFolder.monitoring.searchAll,
        data: payload
      });
    },
    getAllByFolderIdV2: (id: number) => {
      return Api.get({
        url: CONST.documentFolder.monitoring.documentMonitoringFile(id),
        data: {}
      });
    },
    getAllByFolderIdUploadV2: (id: number) => {
      return Api.get({
        url: CONST.documentFolder.monitoring.documentMonitoringFileUploaded(id),
        data: {}
      });
    },
    getAllByFolderId: (id: number) => {
      return axiosClient<DocumentMonitoringFileDTO[]>(
        addHeader({
          method: "GET",
          url: `/document-monitoring-file/folder/${id}`
        })
      );
    },
    getByFolderId: (id: number, payload: IDocumentSearchForm) => {
      return Api.get({
        url: CONST.documentFolder.monitoring.getByFolderId(id),
        data: payload
      });
    },
    getById: (id: number) => {
      return axiosClient<DocumentMonitoringFileDTO>(
        addHeader({
          method: "GET",
          url: `/document-monitoring-file/${id}`
        })
      );
    },
    createFolder: (payload: IDocumentFolderMonitoring) => {
      return Api.post({
        url: CONST.documentFolder.monitoring.createFolder,
        data: payload
      });
    },
    createUploadFile: (payload: FormData) => {
      return Api.post({
        url: CONST.documentFolder.monitoring.createUploadFile,
        data: payload
      });
    },
    create: (documentMonitoringFile: DocumentMonitoringFileDTO) => {
      return axiosClient<DocumentMonitoringFileDTO>(
        addHeader({
          method: "POST",
          url: "/document-monitoring-file/create",
          headers: { "Content-Type": "multipart/form-data" },
          data: documentMonitoringFile
        })
      );
    },
    updateUploadFile: (payload: FormData) => {
      return Api.post({
        url: CONST.documentFolder.monitoring.updateUploadFile,
        data: payload
      });
    },
    updateByFolderId: (payload: IDocumentMonitoringApiData) => {
      return Api.post({
        url: CONST.documentFolder.monitoring.updateByFolderId,
        data: payload
      });
    },
    update: (documentMonitoringFile: DocumentMonitoringFileDTO) => {
      return axiosClient<DocumentMonitoringFileDTO>(
        addHeader({
          method: "POST",
          url: "/document-monitoring-file/update",
          headers: { "Content-Type": "multipart/form-data" },
          data: documentMonitoringFile
        })
      );
    },
    deleteUploadFile: (payload: DocumentMonitoringFileDTO) => {
      return Api.post({
        url: CONST.documentFolder.monitoring.deleteUploadFile,
        data: payload
      });
    },
    delete: (documentMonitoringFile: DocumentMonitoringFileDTO) => {
      return axiosClient<DocumentMonitoringFileDTO>(
        addHeader({
          method: "POST",
          url: "/document-monitoring-file/delete",
          data: documentMonitoringFile
        })
      );
    }
  },
  documentType: {
    getAllV2: () => {
      return Api.get({
        url: CONST.documentFolder.documentType.documentTypeId,
        data: {}
      });
    },
    getAllDocumentTypeCompliance: () => {
      return Api.get({
        url: CONST.documentFolder.documentType.documentTypeCompliance,
        data: {}
      });
    },
    getAllDocumentTypeMornitoring: () => {
      return Api.get({
        url: CONST.documentFolder.documentType.documentTypeMonitoring,
        data: {}
      });
    },
    getAll: () => {
      return axiosClient<DocumentType[]>(
        addHeader({
          method: "GET",
          url: "/document-type/all"
        })
      );
    }
  },
  documentStandardType: {
    getAllV2: () => {
      return Api.get({
        url: CONST.documentFolder.documentType.documentStandardTypeId,
        data: {}
      });
    },
    getAll: () => {
      return axiosClient<DocumentType[]>(
        addHeader({
          method: "GET",
          url: "/document-type/standard/all"
        })
      );
    }
  },
  application: {
    searchAll: (payload: IApplicationSearchForm) => {
      return Api.post({
        url: CONST.application.search,
        data: payload
      });
    },
    getById: (applicationId: number) => {
      return Api.get({
        url: CONST.application.detail(applicationId)
      });
    },
    create: (payload: ApplicationDetail) => {
      return Api.post({
        url: CONST.application.create,
        data: payload
      });
    },
    update: (payload: ApplicationDetail) => {
      return Api.post({
        url: CONST.application.update,
        data: payload
      });
    },
    updateStatus: (payload: ProductStatusUpdate) => {
      return Api.post({
        url: CONST.application.updateStatus,
        data: payload
      });
    },
    delete: (payload: ApplicationDetail) => {
      return Api.deleteData({
        url: CONST.application.delete,
        data: payload
      });
    },
    getByAppNo: (applicationId: string) => {
      return Api.get({
        url: CONST.application.check(applicationId)
      });
    }
  },
  applicationScheme: {
    getAll: () => {
      return axiosClient<ApplicationScheme[]>(
        addHeader({
          method: "GET",
          url: "/application/scheme/all"
        })
      );
    }
  },
  applicationType: {
    getAll: () => {
      return axiosClient<ApplicationType[]>(
        addHeader({
          method: "GET",
          url: "/application/type/all"
        })
      );
    }
  },
  applicationStatus: {
    getAll: () => {
      return axiosClient<ApplicationStatus[]>(
        addHeader({
          method: "GET",
          url: "/application/status/all"
        })
      );
    }
  },
  halalFileStatus: {
    getAll: () => {
      return axiosClient<HalalFileStatus[]>(
        addHeader({
          method: "GET",
          url: "/halal-file/status/all"
        })
      );
    }
  },
  halalFile: {
    getChecklist: (applicationId: number) => {
      return Api.get({
        url: CONST.halalFile.getChecklist(applicationId)
      });
    },
    getById: (applicationId: number) => {
      return Api.get({
        url: CONST.halalFile.detail(applicationId)
      });
    },
    getByHalalFileId: (applicationId: number) => {
      return Api.get({
        url: CONST.halalFile.view(applicationId)
      });
    },
    getApplication: (applicationId: number) => {
      return Api.get({
        url: CONST.halalFile.getApplication(applicationId)
      });
    },
    getFilelist: (payload: ISearchHalalFile) => {
      return Api.post({
        url: CONST.halalFile.getFileList,
        data: payload
      });
    },
    getDocumentlist: () => {
      return Api.get({
        url: CONST.halalFile.getDocumentList
      });
    },
    getCoverLetter: (applicationId: number) => {
      return Api.get({
        url: CONST.halalFile.getCoverLetter(applicationId)
      });
    },
    createCoverLetter: (payload: HalalFileCoverLetter) => {
      return Api.post({
        url: CONST.halalFile.createCoverLetter,
        data: payload
      });
    },
    updateCoverLetter: (payload: HalalFileCoverLetter) => {
      return Api.post({
        url: CONST.halalFile.updateCoverLetter,
        data: payload
      });
    },
    updateCoverOption: (payload: HalalFileCoverLetter) => {
      return Api.post({
        url: CONST.halalFile.updateCoverOption,
        data: payload
      });
    },
    getRawMaterialList: (applicationId: number) => {
      return Api.get({
        url: CONST.halalFile.getRawMaterialList(applicationId)
      });
    },
    getRawMaterialCertList: (applicationId: number) => {
      return Api.get({
        url: CONST.halalFile.getRawMaterialCertList(applicationId)
      });
    },
    create: (payload: HalalFileDetail) => {
      return Api.post({
        url: CONST.halalFile.create,
        data: payload
      });
    },
    delete: (payload: HalalFileDetail) => {
      return Api.deleteData({
        url: CONST.halalFile.delete,
        data: payload
      });
    },
    updateSequence: (payload: HalalFileSequence) => {
      return Api.post({
        url: CONST.halalFile.updateSequence,
        data: payload
      });
    },
    checkRawMaterial: (applicationId: number) => {
      return Api.get({
        url: CONST.halalFile.checkRawMaterial(applicationId)
      });
    },
    generate: (applicationId: number) => {
      return Api.get({
        url: CONST.halalFile.generate(applicationId)
      });
    },
    finalize: (applicationId: number) => {
      return Api.get({
        url: CONST.halalFile.finalize(applicationId)
      });
    },
    getCode: (payload: HalalFileAccessSubmit) => {
      return Api.post({
        url: CONST.halalFile.getcode,
        data: payload
      });
    },
    getAccess: (payload: HalalFileAccessSubmit) => {
      return Api.post({
        url: CONST.halalFile.access,
        data: payload
      });
    },
    addComment: (payload: HalalFileAccessComment) => {
      return Api.post({
        url: CONST.halalFile.addComment,
        data: payload
      });
    },
    updateComment: (payload: HalalFileAccessComment) => {
      return Api.post({
        url: CONST.halalFile.updateComment,
        data: payload
      });
    },
    deleteComment: (payload: HalalFileAccessComment) => {
      return Api.deleteData({
        url: CONST.halalFile.deleteComment,
        data: payload
      });
    },
    searchHalalFileActionLog: (payload: ISearchHalalFileLogRequest) => {
      return Api.post({
        url: CONST.halalFile.searchHalalFileActionLog,
        data: payload
      });
    }
  },
  certification: {
    industryCertification: {
      getIndustryCertificationCategory: () => {
        return Api.get({
          url: CONST.certification.industryCertification.getIndustryCertificationCategory
        });
      },
      searchStatusHistory: (payload: any) => {
        return Api.post({
          url: CONST.certification.industryCertification.searchStatusHistory,
          data: payload
        });
      },
      renewIndustryCertification: (payload: FormData) => {
        return Api.post({
          url: CONST.certification.industryCertification.renewIndustryCertification,
          data: payload
        });
      },
      createIndustryCertification: (payload: FormData) => {
        return Api.post({
          url: CONST.certification.industryCertification.createIndustryCertification,
          data: payload
        });
      },
      updateIndustryCertification: (payload: FormData) => {
        return Api.post({
          url: CONST.certification.industryCertification.updateIndustryCertification,
          data: payload
        });
      },
      getIndustryCertificationById: (id: number) => {
        return Api.get({
          url: CONST.certification.industryCertification.getIndustryCertificationById(id)
        });
      },
      getCertificationName: () => {
        return Api.get({
          url: CONST.certification.industryCertification.getCertificationName
        });
      },
      getCertificateBody: () => {
        return Api.get({
          url: CONST.certification.industryCertification.getCertificateBody
        });
      },
      getAllCertificationStatusV2: () => {
        return Api.get({
          url: CONST.certification.industryCertification.getAllCertificationStatusV2
        });
      },
      searchIndustryCertification: (
        payload: RawMaterialMasterListRequestDTO | RawMaterialMasterListRequestDTOV2
      ) => {
        return Api.post({
          url: CONST.certification.industryCertification.searchIndustryCertification,
          data: payload
        });
      },
      deleteIndustryCertification: (payload: { id: number; organizationId: number }) => {
        return Api.post({
          url: CONST.certification.industryCertification.deleteIndustryCertification,
          data: payload
        });
      }
    }
  },
  auditSchedule: {
    getAuditAuditorTypeV2: () => {
      return Api.get({
        url: CONST.auditSchedule.auditorType
      });
    },
    getAgendasByAuditScheduleIdV2: (id: number) => {
      return Api.get({
        url: CONST.auditSchedule.agenda(id)
      });
    },
    getAuditorsByAuditScheduleIdV2: (id: number) => {
      return Api.get({
        url: CONST.auditSchedule.auditors(id)
      });
    },
    getMiscFilesByAuditScheduleIdV2: (id: number) => {
      return Api.get({
        url: CONST.auditSchedule.file(id)
      });
    },
    internal: {
      searchInternalAuditSchedule: (payload: SearchSupplierAuditScheduleDTO) => {
        return Api.post({
          url: CONST.auditSchedule.internal.search,
          data: payload
        });
      },
      getAuditeeInternalById: (id: number) => {
        return Api.get({
          url: CONST.auditSchedule.internal.auditee(id)
        });
      },
      getCertificationBodyAuditAuditeeInternalById: (id: number) => {
        return Api.get({
          url: CONST.auditSchedule.internal.getCertificationBodyAuditAuditeeInternalById(id)
        });
      },
      getThirdPartyAuditAuditeeInternalById: (id: number) => {
        return Api.get({
          url: CONST.auditSchedule.internal.getThirdPartyAuditAuditeeInternalById(id)
        });
      },
      getDepartmentInternalById: (id: number) => {
        return Api.get({
          url: CONST.auditSchedule.internal.department(id)
        });
      },
      getDepartmentCertificationBodyAuditById: (id: number) => {
        return Api.get({
          url: CONST.auditSchedule.internal.departmentCertificationBodyAudit(id)
        });
      },
      getDepartmentThirdPartyAuditById: (id: number) => {
        return Api.get({
          url: CONST.auditSchedule.internal.departmentThirdPartyAudit(id)
        });
      }
    },
    supplier: {
      search: (payload: SearchSupplierAuditScheduleDTO) => {
        return Api.post({
          url: CONST.auditSchedule.supplier.search,
          data: payload
        });
      },
      getDetail: (id: number) => {
        return Api.get({
          url: CONST.auditSchedule.supplier.detail(id)
        });
      },
      update: (payload: AuditScheduleSupplierEditDTO) => {
        return Api.post({
          url: CONST.auditSchedule.supplier.update,
          data: payload,
          headers: { "Content-Type": "multipart/form-data" }
        });
      },
      create: (payload: AuditScheduleSupplierEditDTO) => {
        return Api.post({
          url: CONST.auditSchedule.supplier.create,
          data: payload,
          headers: { "Content-Type": "multipart/form-data" }
        });
      }
    },
    getAll: () => {
      return axiosClient<AuditSchedule[]>(
        addHeader({
          method: "GET",
          url: "/audit/schedule/all"
        })
      );
    },
    getAuditScheduleInternal: (payload: AuditInternalSearch) => {
      return axiosClient<AuditScheduleInternals>(
        addHeader({
          method: "POST",
          // url: "/audit/schedule/internal/all",
          url: "/audit/schedule/internal/search",
          data: payload
        })
      );
    },
    getAuditScheduleSupplier: (payload: AuditSupplierSearch) => {
      return axiosClient<AuditScheduleSuppliers>(
        addHeader({
          method: "POST",
          // url: "/audit/schedule/supplier/all"
          url: "/audit/schedule/supplier/search",
          data: payload
        })
      );
    },
    getInternalByAuditScheduleId: (id: number) => {
      return Api.get({
        url: CONST.auditSchedule.internal.detail(id)
      });
    },
    getAuditScheduleInternalByAuditScheduleId: (id: number) => {
      return axiosClient<AuditScheduleInternalDTO>(
        addHeader({
          method: "GET",
          url: `/audit/schedule/internal/${id}`
        })
      );
    },
    getAuditScheduleInternalByAuditScheduleIdV2: (id: number) => {
      return Api.get({
        url: CONST.auditSchedule.getAuditScheduleInternalByAuditScheduleId(id)
      });
    },
    getAuditScheduleSupplierByAuditScheduleId: (id: number) => {
      return axiosClient<AuditScheduleSupplierDTO>(
        addHeader({
          method: "GET",
          url: `/audit/schedule/supplier/${id}`
        })
      );
    },
    getAuditScheduleSupplierByAuditScheduleIdV2: (id: number) => {
      return Api.get({
        url: CONST.auditSchedule.getAuditScheduleSupplierByAuditScheduleId(id)
      });
    },
    getByAuditScheduleId: (id: number) => {
      return axiosClient<AuditAuditor>(
        addHeader({
          method: "GET",
          url: `/audit/schedule/${id}`
        })
      );
    },
    getSiteIdByAuditScheduleId: (id: number) => {
      return axiosClient<number>(
        addHeader({
          method: "GET",
          url: `/audit/schedule/site/${id}`
        })
      );
    },
    getAgendasByAuditScheduleId: (id: number) => {
      return axiosClient<AuditAgenda[]>(
        addHeader({
          method: "GET",
          url: `/audit/schedule/agenda/${id}`
        })
      );
    },
    getAuditorsByAuditScheduleId: (id: number) => {
      return axiosClient<AuditAuditor[]>(
        addHeader({
          method: "GET",
          url: `/audit/schedule/auditor/${id}`
        })
      );
    },
    getMiscFilesByAuditScheduleId: (id: number) => {
      return axiosClient<AuditScheduleMiscFileDTO[]>(
        addHeader({
          method: "GET",
          url: `/audit/schedule/file/${id}`
        })
      );
    },
    getDepartmentIdsByAuditScheduleInternalId: (id: number) => {
      return axiosClient<number[]>(
        addHeader({
          method: "GET",
          url: `/audit/schedule/internal/department/${id}`
        })
      );
    },
    getDepartmentIdsByAuditScheduleInternalIdV2: (id: number) => {
      return Api.get({
        url: CONST.auditSchedule.internal.getDepartmentIdsByAuditScheduleInternalIdV2(id)
      });
    },
    getAuditeeIdsByAuditScheduleInternalId: (id: number) => {
      return axiosClient<number[]>(
        addHeader({
          method: "GET",
          url: `/audit/schedule/internal/auditee/${id}`
        })
      );
    },
    getAuditeeIdsByAuditScheduleInternalIdV2: (id: number) => {
      return Api.get({
        url: CONST.auditSchedule.internal.getAuditeeIdsByAuditScheduleInternalIdV2(id)
      });
    },
    createInternalV2: (payload: AuditScheduleInternalEditDTO) => {
      return Api.post({
        url: CONST.auditSchedule.internal.create,
        headers: { "Content-Type": "multipart/form-data" },
        data: payload
      });
    },
    updateInternalV2: (payload: AuditScheduleInternalEditDTO) => {
      return Api.post({
        url: CONST.auditSchedule.internal.update,
        headers: { "Content-Type": "multipart/form-data" },
        data: payload
      });
    },
    createInternal: (auditScheduleInternalEditDTO: AuditScheduleInternalEditDTO) => {
      return axiosClient<AuditSchedule>(
        addHeader({
          method: "POST",
          url: "/audit/schedule/internal/create",
          headers: { "Content-Type": "multipart/form-data" },
          data: auditScheduleInternalEditDTO
        })
      );
    },
    createSupplier: (auditScheduleSupplierEditDTO: AuditScheduleSupplierEditDTO) => {
      return axiosClient<AuditSchedule>(
        addHeader({
          method: "POST",
          url: "/audit/schedule/supplier/create",
          headers: { "Content-Type": "multipart/form-data" },
          data: auditScheduleSupplierEditDTO
        })
      );
    },
    updateInternal: (auditScheduleInternalEditDTO: AuditScheduleInternalEditDTO) => {
      return axiosClient<AuditSchedule>(
        addHeader({
          method: "POST",
          url: "/audit/schedule/internal/update",
          headers: { "Content-Type": "multipart/form-data" },
          data: auditScheduleInternalEditDTO
        })
      );
    },
    updateSupplier: (auditScheduleSupplierEditDTO: AuditScheduleSupplierEditDTO) => {
      return axiosClient<AuditSchedule>(
        addHeader({
          method: "POST",
          url: "/audit/schedule/supplier/update",
          headers: { "Content-Type": "multipart/form-data" },
          data: auditScheduleSupplierEditDTO
        })
      );
    },
    delete: (auditSchedule: AuditSchedule) => {
      return axiosClient<AuditSchedule>(
        addHeader({
          method: "POST",
          url: "/audit/schedule/delete",
          data: auditSchedule
        })
      );
    },
    canDelete: (id: number) => {
      return Api.get({
        url: CONST.auditSchedule.canDelete(id)
      });
    },
    deleteV2: (payload: AuditSchedule) => {
      return Api.post({
        url: CONST.auditSchedule.delete,
        data: payload
      });
    },
    buyerAudit: {
      searchBuyerAuditSchedule: (payload: SearchCertificationBodyAuditScheduleDTO) => {
        return Api.post({
          url: CONST.auditSchedule.buyerAudit.search,
          data: payload
        });
      },
      getBuyerAuditScheduleById: (id: number) => {
        return Api.get({
          url: CONST.auditSchedule.buyerAudit.detail(id)
        });
      },
      getDepartmentBuyerAuditById: (id: number) => {
        return Api.get({
          url: CONST.auditSchedule.buyerAudit.departmentBuyerAudit(id)
        });
      },
      getBuyerAuditAuditeeById: (id: number) => {
        return Api.get({
          url: CONST.auditSchedule.buyerAudit.getBuyerAuditAuditeeById(id)
        });
      },
      updateBuyerAuditSchedule: (payload: AuditScheduleBuyerAuditEditDTO) => {
        return Api.post({
          url: CONST.auditSchedule.buyerAudit.update,
          data: payload
        });
      },
      createBuyerAuditSchedule: (payload: AuditScheduleBuyerAuditEditDTO) => {
        return Api.post({
          url: CONST.auditSchedule.buyerAudit.create,
          data: payload
        });
      }
    }
  },
  certificationBodyAudit: {
    searchCertificationBodyAuditSchedule: (payload: SearchSupplierAuditScheduleDTO) => {
      return Api.post({
        url: CONST.certificationBodyAudit.searchCertificationBodyAuditSchedule,
        data: payload
      });
    },
    searchCertificationBodyAuditReport: (payload: SearchSupplierAuditScheduleDTO) => {
      return Api.post({
        url: CONST.certificationBodyAudit.searchCertificationBodyAuditReport,
        data: payload
      });
    },
    getCertificationBodyAuditScheduleById: (id: number) => {
      return Api.get({
        url: CONST.certificationBodyAudit.getCertificationBodyAuditScheduleById(id)
      });
    },
    getCertificationBodyAuditReportById: (id: number) => {
      return Api.get({
        url: CONST.certificationBodyAudit.getCertificationBodyAuditReportById(id)
      });
    },
    createCertificationBodyAuditSchedule: (payload: SearchCertificationBodyAuditScheduleDTO) => {
      return Api.post({
        url: CONST.certificationBodyAudit.createCertificationBodyAuditSchedule,
        data: payload
      });
    },
    updateCertificationBodyAuditSchedule: (payload: SearchCertificationBodyAuditScheduleDTO) => {
      return Api.post({
        url: CONST.certificationBodyAudit.updateCertificationBodyAuditSchedule,
        data: payload
      });
    },
    getDepartmentCertificationBodyAuditById: (id: number) => {
      return Api.get({
        url: CONST.certificationBodyAudit.getDepartmentCertificationBodyAudit(id)
      });
    }
  },
  thirdPartyAudit: {
    searchThirdPartyAuditSchedule: (payload: SearchSupplierAuditScheduleDTO) => {
      return Api.post({
        url: CONST.thirdPartyAudit.searchThirdPartyAuditSchedule,
        data: payload
      });
    },
    searchThirdPartyAuditReport: (payload: SearchSupplierAuditScheduleDTO) => {
      return Api.post({
        url: CONST.thirdPartyAudit.searchThirdPartyAuditReport,
        data: payload
      });
    },
    getThirdPartyAuditScheduleById: (id: number) => {
      return Api.get({
        url: CONST.thirdPartyAudit.getThirdPartyAuditScheduleById(id)
      });
    },
    getThirdPartyAuditReportById: (id: number) => {
      return Api.get({
        url: CONST.thirdPartyAudit.getThirdPartyAuditReportById(id)
      });
    },
    createThirdPartyAuditSchedule: (payload: SearchThirdPartyAuditScheduleDTO) => {
      return Api.post({
        url: CONST.thirdPartyAudit.createThirdPartyAuditSchedule,
        data: payload
      });
    },
    updateThirdPartyAuditSchedule: (payload: SearchThirdPartyAuditScheduleDTO) => {
      return Api.post({
        url: CONST.thirdPartyAudit.updateThirdPartyAuditSchedule,
        data: payload
      });
    },
    getDepartmentThirdPartyAuditById: (id: number) => {
      return Api.get({
        url: CONST.thirdPartyAudit.getDepartmentThirdPartyAudit(id)
      });
    }
  },
  auditReport: {
    getUnsatisfactoryList: (payload: any) => {
      return Api.post({
        url: CONST.auditReport.getUnsatisfactoryList,
        data: payload
      });
    },
    auditorUploadSignature: (payload: any) => {
      return Api.post({
        url: CONST.auditReport.auditorUploadSignature,
        data: payload
      });
    },
    auditeeUploadSignature: (payload: any) => {
      return Api.post({
        url: CONST.auditReport.auditeeUploadSignature,
        data: payload
      });
    },
    auditReportFinal: (payload: any) => {
      return Api.get({
        url: CONST.auditReport.auditReportFinal,
        data: payload
      });
    },
    auditReportFinalV2: (payload: any) => {
      return Api.post({
        url: CONST.auditReport.auditReportFinalV2,
        data: payload,
        responseType: "blob"
      });
    },
    // auditReportFinalV3: (params: any) => {
    //   return Api.get({
    //     url: CONST.auditReport.auditReportFinal(payload),
    //   });
    // },
    internal: {
      searchInernalAuditReport: (payload: SearchInternalAuditReportDTO) => {
        return Api.post({
          url: CONST.auditReport.internal.search,
          data: payload
        });
      },
      getAllInternalForNonConformance: (payload: any) => {
        return Api.post({
          url: CONST.auditReport.internal.getAllInternalForNonConformance,
          data: payload
        });
      }
    },
    supplier: {
      searchSupplierAuditReport: (payload: SearchSupplierAuditReportDTO) => {
        return Api.post({
          url: CONST.auditReport.supplier.search,
          data: payload
        });
      },
      getAllSupplierForNonConformance: (payload: any) => {
        return Api.post({
          url: CONST.auditReport.supplier.getAllSupplierForNonConformance,
          data: payload
        });
      }
    },
    buyerAudit: {
      searchBuyerAuditReport: (payload: SearchCertificationBodyAuditReportDTO) => {
        return Api.post({
          url: CONST.auditReport.buyerAudit.search,
          data: payload
        });
      }
    },
    conducted: {
      searchAuditConducted: (payload: SearchAuditConductedBody) => {
        return Api.post({
          url: CONST.auditReport.conducted.search,
          data: payload
        });
      },
      createAuditConducted: (payload: CreateAuditConductedBody) => {
        return Api.post({
          url: CONST.auditReport.conducted.create,
          data: payload
        });
      },
      deleteAuditConducted: (params: number) => {
        return Api.deleteData({
          url: CONST.auditReport.conducted.delete(params)
        });
      },
      detailAuditConducted: (params: number) => {
        return Api.get({
          url: CONST.auditReport.conducted.detail(params)
        });
      },
      updatedAuditConducted: (payload: CreateAuditConductedBody) => {
        return Api.post({
          url: CONST.auditReport.conducted.update,
          data: payload
        });
      },
      downloadAuditConducted: (params: number) => {
        return Api.get({
          url: CONST.auditReport.conducted.download(params)
        });
      }
    },
    getAll: () => {
      return axiosClient<AuditReport[]>(
        addHeader({
          method: "GET",
          url: "/audit/report/all"
        })
      );
    },
    getAllV2: (payload: any) => {
      return Api.post({
        url: CONST.auditReport.getAllV2(),
        data: payload
      });
    },
    getAllInternal: () => {
      return axiosClient<AuditReport[]>(
        addHeader({
          method: "GET",
          url: "/audit/report/internal/all"
        })
      );
    },
    getAllSupplier: () => {
      return axiosClient<AuditReport[]>(
        addHeader({
          method: "GET",
          url: "/audit/report/supplier/all"
        })
      );
    },
    getByAuditReportId: (id: number) => {
      return axiosClient<AuditReportEditDTO>(
        addHeader({
          method: "GET",
          url: `/audit/report/${id}`
        })
      );
    },
    getByAuditReportIdV2: (id: number) => {
      return Api.get({
        url: CONST.auditReport.getByAuditReport(id)
      });
    },
    getChecklistResponsesByAuditReportId: (id: number) => {
      return axiosClient<ChecklistResponseDTO[]>(
        addHeader({
          method: "GET",
          url: `/audit/report/${id}/checklist-response`
        })
      );
    },
    getChecklistResponsesByAuditReportIdV2: (id: number) => {
      //ChecklistResponseDTO[]
      return Api.get({
        url: CONST.auditReport.getChecklistResponsesByAuditReportId(id)
      });
    },
    getChecklistUploadsByAuditReportId: (id: number) => {
      return axiosClient<AuditReportChecklistUploadDTO[]>(
        addHeader({
          method: "GET",
          url: `/audit/report/${id}/checklist-upload`
        })
      );
    },
    getChecklistUploadsByAuditReportIdV2: (id: number) => {
      //AuditReportChecklistUploadDTO[]
      return Api.get({
        url: CONST.auditReport.getChecklistUploadsByAuditReportId(id)
      });
    },
    deleteChecklistUploadsByAuditReportIdV2: (id: number) => {
      return Api.deleteData({
        url: CONST.auditReport.DeleteChecklistUploadsByAuditReportId(id)
      });
    },
    getSummaryAuditComplianceChecklist: (id: number) => {
      return Api.get({
        url: CONST.auditReport.getSummaryComplianceChecklistByAuditReportId(id)
      });
    },
    update: (AuditReportEditDTO: AuditReportEditDTO) => {
      return axiosClient<AuditReportEditDTO>(
        addHeader({
          method: "POST",
          url: "/audit/report/update",
          headers: { "Content-Type": "multipart/form-data" },
          data: AuditReportEditDTO
        })
      );
    },
    updatev2: (payload: AuditReportEditDTO) => {
      //AuditReportEditDTO
      return Api.post({
        url: CONST.auditReport.update,
        data: payload,
        headers: { "Content-Type": "multipart/form-data" }
      });
    },

    createChecklistUpload: (auditReportId: number, obj: { checklistFile: File }) => {
      return axiosClient<AuditReportChecklistUploadDTO>(
        addHeader({
          method: "POST",
          url: `/audit/report/${auditReportId}/checklist-upload/create`,
          headers: { "Content-Type": "multipart/form-data" },
          data: obj
        })
      );
    },
    createChecklistUploadV2: (auditReportId: number, obj: { checklistFile: File }) => {
      return Api.post({
        url: CONST.auditReport.createChecklistUpload(auditReportId),
        data: obj,
        headers: { "Content-Type": "multipart/form-data" }
      });
    },
    updateChecklistResponseStatus: (checklistResponseId: number, statusId: number) => {
      return axiosClient<AuditReportChecklistUploadDTO>(
        addHeader({
          method: "POST",
          url: `/audit/report/checklist-response/${checklistResponseId}/status/update?statusId=${statusId}`
        })
      );
    },
    updateChecklistResponseStatusV2: (checklistResponseId: number, statusId: number) => {
      return Api.post({
        url: CONST.auditReport.updateChecklistResponseStatus(checklistResponseId, statusId)
      });
    },
    updateChecklistUploadStatus: (checklistUploadId: number, statusId: number) => {
      return axiosClient<AuditReportChecklistUploadDTO>(
        addHeader({
          method: "POST",
          url: `/audit/report/checklist-upload/${checklistUploadId}/status/update?statusId=${statusId}`
        })
      );
    },
    updateChecklistUploadStatusV2: (checklistUploadId: number, statusId: number) => {
      return Api.post({
        url: CONST.auditReport.updateChecklistUploadStatus(checklistUploadId, statusId)
      });
    },
    downloadChecklistUpload: (id: number) => {
      return axiosClient<File>(
        addHeader({
          method: "GET",
          url: `/audit/report/checklist-upload/${id}/download`
        })
      );
    },
    getNonConformancesByAuditReportId: (id: number) => {
      return axiosClient<NonConformance[]>(
        addHeader({
          method: "GET",
          url: `/audit/report/${id}/non-conformance`
        })
      );
    },
    getNonConformancesByAuditReportIdV2: (id: number) => {
      //NonConformance[]
      return Api.get({
        url: CONST.auditReport.getNonConformancesByAuditReportId(id)
      });
    }
  },
  auditReportStatus: {
    getAll: () => {
      return Api.get({
        url: `${CONST.auditReportStatus.getAll}`
      });
    },
    getByAuditReportStatusId: (id: number) => {
      return axiosClient<AuditReportStatus>(
        addHeader({
          method: "GET",
          url: `/audit/report/status/${id}`
        })
      );
    }
  },
  auditReportChecklistStatus: {
    getAll: () => {
      return axiosClient<AuditReportChecklistStatus[]>(
        addHeader({
          method: "GET",
          url: "/audit/report/checklist/status/all"
        })
      );
    },
    getAllV2: () => {
      //AuditReportChecklistStatus[]
      return Api.get({
        url: `${CONST.auditReportChecklistStatus.getAll}`
      });
    },
    getByAuditReportStatusId: (id: number) => {
      return axiosClient<AuditReportChecklistStatus>(
        addHeader({
          method: "GET",
          url: `/audit/report/checklist/status/${id}`
        })
      );
    }
  },
  auditType: {
    getAllV2: () => {
      return Api.get({
        url: `${CONST.auditType.getAllV2}`
      });
    },
    getAll: () => {
      return axiosClient<AuditType[]>(
        addHeader({
          method: "GET",
          url: "/audit/type/all"
        })
      );
    },
    getAllByAuditCategoryIdV2: (id: number) => {
      //AuditType[]
      return Api.get({
        url: `${CONST.auditType.auditTypeAll}/${id}`
      });
    },
    getAllByAuditCategoryId: (id: number) => {
      return axiosClient<AuditType[]>(
        addHeader({
          method: "GET",
          url: `/audit/type/all/${id}`
        })
      );
    },
    getByAuditTypeId: (id: number) => {
      return axiosClient<AuditType>(
        addHeader({
          method: "GET",
          url: `/audit/type/${id}`
        })
      );
    }
  },
  certificate: {
    statusHistory: (payload: any) => {
      return Api.post({
        url: CONST.certificate.statusHistory,
        data: payload
      });
    },
    dowloadCertFileByBlobId: (blobId: string) => {
      return Api.get({
        url: CONST.certificate.dowloadCertFileByBlobId(blobId),
        responseType: "blob"
      });
    },
    acceptSupplier: (payload: ICertificateRequestSupplierAccept) => {
      return Api.post({
        url: CONST.certificate.supplier.accept,
        data: payload
      });
    },
    rejectSupplier: (payload: ICertificateRequestSupplierReject) => {
      return Api.post({
        url: CONST.certificate.supplier.reject,
        data: payload
      });
    },
    searchCertificationBody: (payload: ISearchCertificateBody) => {
      return Api.post({
        url: CONST.certificate.certificateBody.searchAll,
        data: payload
      });
    },
    searchAllCertificationBody: (payload: ISearchCertificateBody) => {
      return Api.post({
        url: CONST.certificate.certificateBody.searchAllV2,
        data: payload
      });
    },
    searchCertificationBodyV2: (payload: ISearchCertificateBody) => {
      return Api.post({
        url: CONST.certificate.certificateBody.searchAllV2,
        data: payload
      });
    },
    searchCertificationStatus: () => {
      return Api.get({
        url: CONST.certificate.certificateBody.searchStatus
      });
    },
    getByCertificationBodyId: (id: number) => {
      return Api.get({
        url: CONST.certificate.certificateBody.detail(id)
      });
    },
    createCertificationBody: (payload: CertificationBody) => {
      return Api.post({
        url: CONST.certificate.certificateBody.create,
        data: payload
      });
    },
    updateCertificationBody: (payload: CertificationBody) => {
      return Api.post({
        url: CONST.certificate.certificateBody.update,
        data: payload
      });
    },
    deleteCertificationBody: (payload: CertificationBody) => {
      return Api.post({
        url: CONST.certificate.certificateBody.delete,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<CertificateDTO[]>(
        addHeader({
          method: "GET",
          url: "/certificate/all"
        })
      );
    },
    getAllRawMaterialCerts: (payload: IRawMaterialCertMgmt) => {
      return Api.post({
        url: CONST.certificate.rawMaterialCertificate.searchAll,
        data: payload
      });
    },
    getAllProductCertsV2: (
      payload: ISearchProductCertsBody
    ): Promise<CustomSuccessResponse<CertificateDTO[]>> => {
      return Api.post({
        url: CONST.certificate.product,
        data: payload
      });
    },
    searchProductAssociated: (payload: ISearchAssociatedCertificateProductPayload) => {
      return Api.post({
        url: CONST.certificate.productSearchAssociated,
        data: payload
      });
    },
    getAllTradingProductCertsV2: (
      payload: ISearchProductCertsBody
    ): Promise<CustomSuccessResponse<CertificateDTO[]>> => {
      return Api.post({
        url: CONST.certificateProductTrading.search,
        data: payload
      });
    },
    searchTradingProductAssociated: (payload: ISearchAssociatedCertificateProductPayload) => {
      return Api.post({
        url: CONST.certificateProductTrading.searchAssociated,
        data: payload
      });
    },
    getAllFoodPremiseCertsV2: (
      payload: ISearchProductCertsBody
    ): Promise<CustomSuccessResponse<CertificateDTO[]>> => {
      return Api.post({
        url: CONST.certificateFoodPremise.search,
        data: payload
      });
    },
    searchFoodPremiseAssociatedMenus: (payload: ISearchAssociatedCertificateProductPayload) => {
      return Api.post({
        url: CONST.certificateFoodPremise.searchAssociated,
        data: payload
      });
    },
    searchFoodPremiseAssociatedRawMaterials: (
      payload: ISearchAssociatedCertificateProductPayload
    ) => {
      return Api.post({
        url: CONST.certificateFoodPremise.searchAssociatedRawMaterial,
        data: payload
      });
    },
    getAllProductCerts: () => {
      return axiosClient<CertificateDTO[]>(
        addHeader({
          method: "GET",
          url: "/certificate/product/all"
        })
      );
      // Api.post({
      //   url: "/v2/api/certificate/product/search",
      //   data
      // });
    },
    getCertificateById: (id: number) => {
      return Api.get({
        url: CONST.certificate.rawMaterialCertificate.getById(id)
      });
    },
    getTradingProductCertByIdV2: (certificateId: number) => {
      return Api.get({
        url: CONST.certificateProductTrading.getById(certificateId)
      });
    },
    getFoodPremiseCertByIdV2: (certificateId: number) => {
      return Api.get({
        url: CONST.certificateFoodPremise.getById(certificateId)
      });
    },
    getById: (certId: number) => {
      return Api.get({
        url: `${CONST.certificate.basePath}/${certId}`
      });
      // axiosClient<CertificateDTO>(
      //   addHeader({
      //     method: "GET",
      //     url: `/certificate/${certId}`
      //   })
      // );
    },
    downloadCerificateFile: (certId: number) => {
      return axiosClient(
        addHeader({
          method: "GET",
          url: `/certificate/${certId}/file/download`
        })
      );
    },
    getLinkage: (certId: number) => {
      return axiosClient<boolean>(
        addHeader({
          method: "GET",
          url: `/certificate/delete/${certId}/linked`
        })
      );
    },
    createV2: (newCertificate: CertificateDTO) => {
      return Api.post({
        url: CONST.certificate.create,
        headers: { "Content-Type": "multipart/form-data" },
        data: newCertificate
      });
    },
    createTradingProductCertV2: (newCertificate: FormData) => {
      return Api.post({
        url: CONST.certificateProductTrading.create,
        headers: { "Content-Type": "multipart/form-data" },
        data: newCertificate
      });
    },
    createFoodPremiseCertV2: (newCertificate: FormData) => {
      return Api.post({
        url: CONST.certificateFoodPremise.create,
        headers: { "Content-Type": "multipart/form-data" },
        data: newCertificate
      });
    },
    createRawMaterialCertificate: (payload: FormData) => {
      return Api.post({
        url: CONST.certificate.rawMaterialCertificate.create,
        data: payload
      });
    },
    create: (newCertificate: CertificateDTO) => {
      return Api.post({
        url: "/v2/api/certificate/create",
        headers: { "Content-Type": "multipart/form-data" },
        data: newCertificate
      });
      // axiosClient<CertificateDTO>(
      //   addHeader({
      //     method: "POST",
      //     url: "/certificate/create",
      //     headers: { "Content-Type": "multipart/form-data" },
      //     data: newCertificate
      //   })
      // );
    },
    updateRawMaterialCertificate: (payload: FormData) => {
      return Api.post({
        url: CONST.certificate.rawMaterialCertificate.update,
        data: payload
      });
    },
    updateTradingProductCertV2: (payload: FormData) => {
      return Api.post({
        url: CONST.certificateProductTrading.update,
        data: payload
      });
    },
    updateFoodPremiseCertV2: (payload: FormData) => {
      return Api.post({
        url: CONST.certificateFoodPremise.update,
        data: payload
      });
    },
    update: (certificate: CertificateDTO) => {
      return Api.put({
        url: "/v2/api/certificate/update",
        headers: { "Content-Type": "multipart/form-data" },
        data: certificate
      });
      // axiosClient<CertificateDTO>(
      //   addHeader({
      //     method: "POST",
      //     url: "/certificate/update",
      //     headers: { "Content-Type": "multipart/form-data" },
      //     data: certificate
      //   })
      // );
    },
    deleteRawMaterialCertificate: (certId: number, orgId: number) => {
      return Api.deleteData({
        url: CONST.certificate.rawMaterialCertificate.delete(certId, orgId)
      });
    },
    delete: (certId: number, orgId: number) => {
      return Api.deleteData({
        url: `/v2/api/certificate/delete/${certId}/${orgId}`
      });
      // axiosClient<boolean>(
      //   addHeader({
      //     method: "DELETE",
      //     url: `/certificate/delete/${certId}/${orgId}`
      //   })
      // );
    },
    deleteTradingProductCert: (certId: number, orgId: number) => {
      return Api.deleteData({
        url: CONST.certificateProductTrading.delete(certId, orgId)
      });
    },
    deleteFoodPremiseCert: (certId: number, orgId: number) => {
      return Api.deleteData({
        url: CONST.certificateFoodPremise.delete(certId, orgId)
      });
    },
    getAllCertificationTypesV2: () => {
      return Api.get({
        url: CONST.certificate.certificateType.searchAll
      });
    },
    getAllCertificationTypes: () => {
      return axiosClient<CertificateType[]>(
        addHeader({
          method: "GET",
          url: "/certificate/type/all"
        })
      );
    },
    getCertificationTypeById: (certTypeId: number) => {
      return axiosClient<CertificateType>(
        addHeader({
          method: "GET",
          url: `/certificate/type/${certTypeId}`
        })
      );
    },
    getAllCertificationBodies: () => {
      return axiosClient<CertificationBody[]>(
        addHeader({
          method: "GET",
          url: "/certificate/body/all"
        })
      );
    },
    getAllCertificationBodiesV2: () => {
      return Api.get({
        url: CONST.certificate.certificateBody.getAll
      });
    },
    getCertificationBodyById: (certBodyId: number) => {
      return axiosClient<CertificationBody>(
        addHeader({
          method: "GET",
          url: `/certificate/body/${certBodyId}`
        })
      );
    }
  },
  checklistTemplate: {
    getAllByChecklistTypeIdV2: (payload: SearchChecklistRequestDTO) => {
      //ChecklistTemplateDTO[]
      return Api.post({
        url: CONST.checklistTemplate.search,
        data: payload
      });
    },
    getAllByChecklistTypeId: (id: number) => {
      return axiosClient<ChecklistTemplateDTO[]>(
        addHeader({
          method: "GET",
          url: `/checklist/template/all/${id}`
        })
      );
    },
    getByChecklistTemplateId: (id: number) => {
      return axiosClient<ChecklistTemplateDTO>(
        addHeader({
          method: "GET",
          url: `/checklist/template/${id}`
        })
      );
    },
    details: (id: number) => {
      return Api.get({
        url: CONST.checklistTemplate.detail(id)
      });
    },
    create: (checklistTemplateEditDTO: ChecklistTemplateEditDTO) => {
      return Api.post({
        url: CONST.checklistTemplate.create,
        data: checklistTemplateEditDTO,
        headers: { "Content-Type": "multipart/form-data" }
      });
    },
    createnew: (checklistTemplateEditDTO: ChecklistTemplateEditDTO) => {
      return Api.post({
        url: CONST.checklistTemplate.createnew,
        data: checklistTemplateEditDTO
      });
    },
    update: (checklistTemplateEditDTO: ChecklistTemplateEditDTO) => {
      return Api.post({
        url: CONST.checklistTemplate.update,
        data: checklistTemplateEditDTO,
        headers: { "Content-Type": "multipart/form-data" }
      });
    },
    updatenew: (checklistTemplateEditDTO: ChecklistTemplateEditDTO) => {
      return Api.post({
        url: CONST.checklistTemplate.updatenew,
        data: checklistTemplateEditDTO
      });
    },
    delete: (checklist: ChecklistTemplateDTO) => {
      return axiosClient<ChecklistTemplateDTO>(
        addHeader({
          method: "POST",
          url: "/checklist/template/delete",
          data: checklist
        })
      );
    },
    deleteChecklistTemplateV2: (payload: ChecklistTemplateDTO) => {
      return Api.post({
        url: CONST.checklistTemplate.delete,
        data: payload
      });
    },
    uploadTemplate: (payload: any) => {
      return Api.post({
        url: CONST.checklistTemplate.uploadTemplate,
        data: payload
      });
    },
    downloadTemplate: () => {
      return Api.get({
        url: CONST.checklistTemplate.downloadTemplate
      });
    }
  },
  checklistTemplateRevision: {
    getSectionsByRevisionId: (id: number) => {
      return Api.get({
        url: CONST.checklistTemplateRevision.getSectionByRevisionId(id)
      });
    },
    getSectionsByChecklistTemplateRevisionId: (id: number) => {
      return axiosClient<ChecklistTemplateSection[]>(
        addHeader({
          method: "GET",
          url: `/checklist/template/revision/${id}/sections`
        })
      );
    }
  },
  checklistTemplateStatus: {
    getAll: () => {
      return Api.get({
        url: CONST.checklistTemplateStatus.getAll
      });
    },
    getByChecklistTemplateStatusId: (id: number) => {
      return axiosClient<ChecklistTemplateStatus>(
        addHeader({
          method: "GET",
          url: `/checklist/template/status/${id}`
        })
      );
    }
  },
  checklistResponse: {
    details: (id: number) => {
      return Api.get({
        url: CONST.checklistResponse.detail(id)
      });
    },
    deleteChecklistResponseById: (id: number) => {
      return Api.deleteData({
        url: CONST.checklistResponse.detail(id)
      });
    },
    getChecklistResponseById: (id: number) => {
      return axiosClient<ChecklistResponseEditDTO>(
        addHeader({
          method: "GET",
          url: `/checklist/response/${id}`
        })
      );
    },
    createV2: (checklistResponseEditDTO: FormData) => {
      return Api.post({
        url: CONST.checklistResponse.create,
        data: checklistResponseEditDTO
      });
    },
    create: (checklistResponseEditDTO: FormData) => {
      return axiosClient<ChecklistResponseEditDTO>(
        addHeader({
          method: "POST",
          url: "/checklist/response/create",
          //headers: { "Content-Type": "multipart/form-data" },
          data: checklistResponseEditDTO
        })
      );
    },
    updateV2: (checklistResponseEditDTO: FormData) => {
      return Api.post({
        url: CONST.checklistResponse.update,
        data: checklistResponseEditDTO
      });
    },
    update: (checklistResponseEditDTO: FormData) => {
      return axiosClient<ChecklistResponseEditDTO>(
        addHeader({
          method: "POST",
          url: "/checklist/response/update",
          //headers: { "Content-Type": "multipart/form-data" },
          data: checklistResponseEditDTO
        })
      );
    },
    score: (id: number) => {
      return Api.get({
        url: CONST.checklistResponse.score(id)
      });
    },
    scoreall: (id: number) => {
      return Api.get({
        url: CONST.checklistResponse.scoreall(id)
      });
    },
    updateStatus: (payload: FormData) => {
      return Api.post({
        url: CONST.checklistResponse.updateStatus,
        data: payload,
      });
    },
    getStatusHistory: (payload: { checklistResponseId: number; userOrganizationId: number }) => {
      return Api.post({
        url: CONST.checklistResponse.statusHistory,
        data: payload
      });
    }
  },
  nonConformance: {
    getStatusHistory: (payload: any) => {
      return Api.post({
        url: CONST.nonconformance.statusHistory,
        data: payload
      });
    },
    detail: (id: number) => {
      return Api.get({
        url: CONST.nonconformance.detail(id)
      });
    },
    getAssigneeList: (id: number) => {
      return Api.get({
        url: CONST.nonconformance.assignList(id)
      });
    },
    getAll: () => {
      return axiosClient<NonConformance[]>(
        addHeader({
          method: "GET",
          url: "/non-conformance/all"
        })
      );
    },
    search: (payload: NonConformanceTypeRequestDTO) => {
      return Api.post({
        url: CONST.nonconformance.search,
        data: payload
      });
    },
    getByNonConformanceId: (id: number) => {
      return axiosClient<NonConformanceEditDTO>(
        addHeader({
          method: "GET",
          url: `/non-conformance/${id}`
        })
      );
    },
    create: (nonConformanceEditDTO: NonConformanceEditDTO) => {
      return axiosClient<NonConformanceEditDTO>(
        addHeader({
          method: "POST",
          url: "/non-conformance/create",
          headers: { "Content-Type": "multipart/form-data" },
          data: nonConformanceEditDTO
        })
      );
    },
    createV2: (payload: NonConformanceEditDTOV2) => {
      return Api.post({
        url: CONST.nonconformance.create,
        headers: { "Content-Type": "multipart/form-data" },
        data: payload
      });
    },
    update: (nonConformanceEditDTO: NonConformanceEditDTO) => {
      return axiosClient<NonConformanceEditDTO>(
        addHeader({
          method: "POST",
          url: "/non-conformance/update",
          headers: { "Content-Type": "multipart/form-data" },
          data: nonConformanceEditDTO
        })
      );
    },
    updateV2: (payload: NonConformanceEditDTO) => {
      return Api.post({
        url: CONST.nonconformance.update,
        headers: { "Content-Type": "multipart/form-data" },
        data: payload
      });
    },
    delete: (nonConformance: NonConformance) => {
      return axiosClient<NonConformance>(
        addHeader({
          method: "POST",
          url: "/non-conformance/delete",
          data: nonConformance
        })
      );
    },
    deleteV2: (payload: NonConformance) => {
      return Api.post({
        url: CONST.nonconformance.delete,
        data: payload
      });
    }
  },
  nonConformanceType: {
    getAll: () => {
      return axiosClient<NonConformanceType[]>(
        addHeader({
          method: "GET",
          url: "/non-conformance/type/all"
        })
      );
    },
    getAllv2: () => {
      //NonConformanceType[]
      return Api.get({
        url: CONST.nonconformancetype.searchall
      });
    }
  },
  nonConformanceGrading: {
    getAll: () => {
      return axiosClient<NonConformanceGrading[]>(
        addHeader({
          method: "GET",
          url: "/non-conformance/grading/all"
        })
      );
    },
    getAllv2: () => {
      //NonConformanceGrading[]
      return Api.get({
        url: CONST.nonConformanceGrading.searchall
      });
    }
  },
  nonConformanceStatus: {
    getAll: () => {
      return axiosClient<NonConformanceStatus[]>(
        addHeader({
          method: "GET",
          url: "/non-conformance/status/all"
        })
      );
    },
    getAllv2: () => {
      //NonConformanceStatus[]
      return Api.get({
        url: CONST.nonConformanceStatus.searchall
      });
    }
  },
  certificateRequest: {
    searchAll: (payload: IOutgoingCertReq) => {
      return Api.post({
        url: CONST.certificate.outgoingCertificateReq.searchAll,
        data: payload
      });
    },
    searchAllSupplier: (payload: ICertificateRequestSupplierSearchForm) => {
      return Api.post({
        url: CONST.certificate.supplier.searchAll,
        data: payload
      });
    },
    searchAllAdmin: (payload: ICertificateRequestSupplierSearchForm) => {
      return Api.post({
        url: CONST.certificate.admin.searchAll,
        data: payload
      });
    },
    getSupplierByCertificateRequestId: (id: number) => {
      return Api.get({
        url: CONST.certificate.supplier.detail(id)
      });
    },
    downloadFileCert: (id1: number, id2: number) => {
      return Api.get({
        url: CONST.certificate.downloadFileId(id1, id2)
      });
    },
    downloadFileCert2: (id1: number, id2: number) => {
      return Api.get({
        url: CONST.certificate.downloadFileId(id1, id2),
        responseType: "blob"
      });
    },
    downloadFileCertVer2: (certificateRequestId: number) => {
      return Api.get({
        url: CONST.certificate.downloadFileIdVer2(certificateRequestId),
        responseType: "blob"
      });
    },
    getAllOutgoing: () => {
      return axiosClient<CertificateRequestDTO[]>(
        addHeader({
          method: "GET",
          url: "/certificate-request/outgoing/all"
        })
      );
    },
    searchAllIncoming: (payload: IIncomingCertReq) => {
      return Api.post({
        url: CONST.certificate.incomingCertificateReq.searchAll,
        data: payload
      });
    },
    getAllIncoming: () => {
      return axiosClient<CertificateRequestDTO[]>(
        addHeader({
          method: "GET",
          url: "/certificate-request/incoming/all"
        })
      );
    },
    getOutgoingByCertificateRequestId: (id: number) => {
      return axiosClient<CertificateRequestDTO>(
        addHeader({
          method: "GET",
          url: `/certificate-request/outgoing/${id}`
        })
      );
    },
    getIncomingByCertificateRequestId: (id: number) => {
      return axiosClient<CertificateRequestDTO>(
        addHeader({
          method: "GET",
          url: `/certificate-request/incoming/${id}`
        })
      );
    },
    create: (certificateRequest: CertificateRequestDTO) => {
      return axiosClient<CertificateRequestDTO>(
        addHeader({
          method: "POST",
          url: "/certificate-request/create",
          data: certificateRequest
        })
      );
    },
    createV2: (payload: ICreateCertificateRequest) => {
      return Api.post({
        url: CONST.certificate.createV2,
        data: payload
      });
    },
    update: (certificateRequest: CertificateRequestDTO) => {
      return axiosClient<CertificateRequestDTO>(
        addHeader({
          method: "POST",
          url: "/certificate-request/update",
          data: certificateRequest
        })
      );
    },
    uploadSupplier: (payload: FormData) => {
      return Api.post({
        url: CONST.certificate.supplier.upload,
        data: payload
      });
    },
    accept: (certificateRequest: CertificateRequestDTO) => {
      return axiosClient<CertificateRequestDTO>(
        addHeader({
          method: "POST",
          url: "/certificate-request/accept",
          data: certificateRequest
        })
      );
    },
    getRemarksByCertificateRequestId: (id: number) => {
      return axiosClient<CertificateRequestRemarkDTO[]>(
        addHeader({
          method: "GET",
          url: `/certificate-request/${id}/remark`
        })
      );
    },
    supplier: {
      searchCountTab: (payload: ICertificateRequestSupplierSearchForm) => {
        return Api.post({
          url: CONST.certificate.supplier.searchCountTab,
          data: payload
        });
      }
    },
    admin: {
      searchCountTab: (payload: ICertificateRequestSupplierSearchForm) => {
        return Api.post({
          url: CONST.certificate.admin.searchCountTab,
          data: payload
        });
      },
      confirm: (payload: ICertificateRequestAdminConfim) => {
        return Api.post({
          url: CONST.certificate.admin.confirm,
          data: payload
        });
      },
      reject: (payload: ICertificateRequestAdminReject) => {
        return Api.post({
          url: CONST.certificate.admin.reject,
          data: payload
        });
      },
      getById: (id: number) => {
        return Api.get({
          url: CONST.certificate.admin.detail(id)
        });
      },
      remarkList: (id: number) => {
        return Api.get({
          url: CONST.certificate.admin.remarkList(id)
        });
      }
    }
  },
  certificateRequestRemark: {
    getById: (id: number) => {
      return axiosClient<CertificateRequestRemarkDTO>(
        addHeader({
          method: "GET",
          url: `/certificate-request/remark/${id}`
        })
      );
    },
    create: (certificateRequestRemark: CertificateRequestRemarkDTO) => {
      return axiosClient<CertificateRequestRemarkDTO>(
        addHeader({
          method: "POST",
          headers: { "Content-Type": "multipart/form-data" },
          url: "/certificate-request/remark/create",
          data: certificateRequestRemark
        })
      );
    },
    update: (certificateRequestRemark: CertificateRequestRemarkDTO) => {
      return axiosClient<CertificateRequestRemarkDTO>(
        addHeader({
          method: "POST",
          headers: { "Content-Type": "multipart/form-data" },
          url: "/certificate-request/remark/update",
          data: certificateRequestRemark
        })
      );
    },
    downloadRemarkFileById: (id: number) => {
      return axiosClient<File>(
        addHeader({
          method: "GET",
          url: `/certificate-request/remark/${id}/file/download`
        })
      );
    }
  },
  certificateRequestStatus: {
    searchAll: () => {
      return Api.get({
        url: CONST.certificate.status.searchAll
      });
    },
    getByStatusId: (id: number) => {
      return Api.get({
        url: CONST.certificate.status.detail(id)
      });
    },
    getAll: () => {
      return axiosClient<CertificateRequestStatus[]>(
        addHeader({
          method: "GET",
          url: "/certificate-request/status/all"
        })
      );
    }
  },
  notification: {
    getUnread: (id: number) => {
      return Api.get({
        url: CONST.notification.unreadByUserId(id)
      });
    },
    getRead: (id: number) => {
      return Api.get({
        url: CONST.notification.readByUserId(id)
      });
    },
    markAsRead: (notificationId: number) => {
      return Api.post({
        url: CONST.notification.markAsReadByNotiId(notificationId)
      });
    },
    search: (payload: SearchNotificationDTO) => {
      return Api.post({
        url: CONST.notification.search,
        data: payload
      });
    },
    markAsReadAll: (userId: number) => {
      return Api.post({
        url: CONST.notification.markAsReadAllByUserId(userId)
      });
    }
  },
  department: {
    searchAll: (payload: IDepartmentSearchForm) => {
      return Api.post({
        url: CONST.department.search,
        data: payload
      });
    },
    getAllByOrganizationIdV2: (orgId: number) => {
      return Api.get({
        url: CONST.department.departmentByOrgId(orgId)
      });
    },
    getAllByOrganizationId: (currOrgId: number) => {
      return axiosClient<Department[]>(
        addHeader({
          method: "GET",
          url: `/department/all/organization/${currOrgId}`
        })
      );
    },
    getDepartmentBySiteId: (siteId: number) => {
      return Api.get({
        url: `${CONST.department.departmentBySite}/${siteId}`
      });
    },
    getAllBySiteId: (siteId: number) => {
      return axiosClient<Department[]>(
        addHeader({
          method: "GET",
          url: `/department/all/site/${siteId}`
        })
      );
    },
    getById: (departmentId: number) => {
      return axiosClient<Department>(
        addHeader({
          method: "GET",
          url: `/department/${departmentId}`
        })
      );
    },
    create: (department: Department) => {
      return axiosClient<Department>(
        addHeader({
          method: "POST",
          url: "/department/create",
          headers: { "Content-Type": "application/json" },
          data: department
        })
      );
    },
    createV2: (payload: Department) => {
      return Api.post({
        url: CONST.department.create,
        data: payload
      });
    },
    update: (department: Department) => {
      return axiosClient<Department>(
        addHeader({
          method: "POST",
          url: "/department/update",
          headers: { "Content-Type": "application/json" },
          data: department
        })
      );
    },
    updateV2: (payload: Department) => {
      return Api.post({
        url: CONST.department.update,
        data: payload
      });
    },
    delete: (department: Department) => {
      return axiosClient<Department>(
        addHeader({
          method: "POST",
          url: "/department/delete",
          headers: { "Content-Type": "application/json" },
          data: department
        })
      );
    },
    deleteV2: (payload: Department) => {
      return Api.post({
        url: CONST.department.delete,
        data: payload
      });
    }
  },
  designation: {
    searchAll: (payload: IDesignationSearchForm) => {
      return Api.post({
        url: CONST.designation.search,
        data: payload
      });
    },
    getAll: (currOrgId: number) => {
      return axiosClient<Designation[]>(
        addHeader({
          method: "GET",
          url: `/designation/all/organization/${currOrgId}`
        })
      );
    },
    getById: (designationId: number) => {
      return axiosClient<Designation>(
        addHeader({
          method: "GET",
          url: `/designation/${designationId}`
        })
      );
    },
    getAllBySiteId: (siteId: number) => {
      return axiosClient<Designation[]>(
        addHeader({
          method: "GET",
          url: `/designation/all/site/${siteId}`
        })
      );
    },
    create: (designation: Designation) => {
      return axiosClient<Designation>(
        addHeader({
          method: "POST",
          url: "/designation/create",
          headers: { "Content-Type": "application/json" },
          data: designation
        })
      );
    },
    createV2: (payload: Designation) => {
      return Api.post({
        url: CONST.designation.create,
        data: payload
      });
    },
    update: (designation: Designation) => {
      return axiosClient<Designation>(
        addHeader({
          method: "POST",
          url: "/designation/update",
          headers: { "Content-Type": "application/json" },
          data: designation
        })
      );
    },
    updateV2: (payload: Designation) => {
      return Api.post({
        url: CONST.designation.update,
        data: payload
      });
    },
    delete: (designation: Designation) => {
      return axiosClient<Designation>(
        addHeader({
          method: "POST",
          url: "/designation/delete",
          headers: { "Content-Type": "application/json" },
          data: designation
        })
      );
    },
    deleteV2: (payload: Designation) => {
      return Api.post({
        url: CONST.designation.delete,
        data: payload
      });
    }
  },
  employeeGroup: {
    searchAll: (payload: IEmployeeGroupSearchForm) => {
      return Api.post({
        url: CONST.employeegroup.search,
        data: payload
      });
    },
    getAll: (currOrgId: number) => {
      return axiosClient<EmployeeGroup[]>(
        addHeader({
          method: "GET",
          url: `/employee-group/organization/${currOrgId}`
        })
      );
    },
    getAllBySiteId: (siteId: number) => {
      return axiosClient<EmployeeGroup[]>(
        addHeader({
          method: "GET",
          url: `/employee-group/site/${siteId}`
        })
      );
    },
    getById: (employeeGroupId: number) => {
      return axiosClient<EmployeeGroup>(
        addHeader({
          method: "GET",
          url: `/employee-group/${employeeGroupId}`
        })
      );
    },
    create: (employeeGroup: EmployeeGroup) => {
      return axiosClient<EmployeeGroup>(
        addHeader({
          method: "POST",
          url: "/employee-group/create",
          headers: { "Content-Type": "application/json" },
          data: employeeGroup
        })
      );
    },
    createV2: (payload: EmployeeGroup) => {
      return Api.post({
        url: CONST.employeegroup.create,
        data: payload
      });
    },
    update: (employeeGroup: EmployeeGroup) => {
      return axiosClient<EmployeeGroup>(
        addHeader({
          method: "POST",
          url: "/employee-group/update",
          headers: { "Content-Type": "application/json" },
          data: employeeGroup
        })
      );
    },
    updateV2: (payload: EmployeeGroup) => {
      return Api.post({
        url: CONST.employeegroup.update,
        data: payload
      });
    },
    delete: (employeeGroup: EmployeeGroup) => {
      return axiosClient<EmployeeGroup>(
        addHeader({
          method: "POST",
          url: "/employee-group/delete",
          headers: { "Content-Type": "application/json" },
          data: employeeGroup
        })
      );
    },
    deleteV2: (payload: EmployeeGroup) => {
      return Api.post({
        url: CONST.employeegroup.delete,
        data: payload
      });
    }
  },
  employee: {
    searchAll: (payload: IEmployeeSearchForm) => {
      return Api.post({
        url: CONST.employee.search,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<Employee[]>(
        addHeader({
          method: "GET",
          url: "/employee/all"
        })
      );
    },
    getById: (employeeId: number) => {
      return axiosClient<Employee>(
        addHeader({
          method: "GET",
          url: `/employee/${employeeId}`
        })
      );
    },
    createV2: (employee: FormData) => {
      return Api.post({
        url: CONST.employee.create,
        data: employee
      });
    },
    updateV2: (employee: FormData) => {
      return Api.post({
        url: CONST.employee.update,
        data: employee
      });
    },
    deleteV2: (employee: Employee) => {
      return Api.post({
        url: CONST.employee.delete,
        data: employee
      });
    },
    downloadPhotoFile: (employeeId: number) => {
      return axiosClient<File>(
        addHeader({
          method: "GET",
          url: `/employee/${employeeId}/download`
        })
      );
    },
    downloadPhotoFileV2: (id: number) => {
      return Api.get({
        url: CONST.employee.employeeDownloadV2(id),
        responseType: "blob"
      });
    },
    getGenderOptions: () => {
      return Api.get({
        url: CONST.employee.getAllGender
      });
    },
    getReligionOptions: () => {
      return Api.get({
        url: CONST.employee.getAllReligion
      });
    },
    getNationalityTypeOptions: () => {
      return Api.get({
        url: CONST.employee.getAllNationalityType
      });
    }
  },
  employeeStatus: {
    getAll: () => {
      return axiosClient<EmployeeStatus[]>(
        addHeader({
          method: "GET",
          url: "/employee-status/all"
        })
      );
    },
    getById: (statusId: number) => {
      return axiosClient<EmployeeStatus>(
        addHeader({
          method: "GET",
          url: `/employee-status/${statusId}`
        })
      );
    }
  },
  /**
   * Employee training master list
   */
  employeeTraining: {
    searchAll: (payload: IEmployeeTrainingSearchForm) => {
      return Api.post({
        url: CONST.employeeTraining.search,
        data: payload
      });
    },
    create: (payload: EmployeeTrainingDetail) => {
      return Api.post({
        url: CONST.employeeTraining.create,
        data: payload
      });
    },
    update: (payload: FormData) => {
      return Api.post({
        url: CONST.employeeTraining.update,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<EmployeeTraining[]>(
        addHeader({
          method: "GET",
          url: "/employee-training"
        })
      );
    },
    getByEmployeeId: (employeeId: number) => {
      return axiosClient<EmployeeTrainingDetail[]>(
        addHeader({
          method: "GET",
          url: `/employee-training/employee/${employeeId}`
        })
      );
    },
    getEmployeeTrainingById: (id: number) => {
      return Api.get({
        url: CONST.employeeTraining.detail(id)
      });
    },
    delete: (payload: EmployeeTrainingDetail) => {
      return Api.post({
        url: CONST.employeeTraining.delete,
        data: payload
      });
    }
  },
  employeeVaccination: {
    /**
     * Employee vaccination master list
     */
    searchAll: (payload: IEmployeeVaccinationSearchForm) => {
      return Api.post({
        url: CONST.employeeVaccination.search,
        data: payload
      });
    },
    deleteV2: (payload: EmployeeVaccinationBase) => {
      return Api.post({
        url: CONST.employeeVaccination.delete,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<EmployeeVaccinationBase[]>(
        addHeader({
          method: "GET",
          url: "/employee-vaccination"
        })
      );
    },
    getByEmployeeId: (employeeId: number) => {
      return axiosClient<EmployeeVaccinationDetail[]>(
        addHeader({
          method: "GET",
          url: `/employee-vaccination/employee/${employeeId}`
        })
      );
    }
  },
  typhoidVaccination: {
    searchAll: (payload: IEmployeeVaccinationSearchForm) => {
      return Api.post({
        url: CONST.typhoidVaccination.search,
        data: payload
      });
    },
    monitoringSearchAll: (payload: IEmployeeVaccinationSearchForm) => {
      return Api.post({
        url: CONST.monitoring.typhoidVaccination.search,
        data: payload
      });
    },
    typhoidVaccinationDownload: (id: number) => {
      return Api.get({
        url: CONST.typhoidVaccination.typhoidVaccinationDownload(id)
      });
    },
    create: (payload: FormData) => {
      return Api.post({
        url: CONST.typhoidVaccination.create,
        data: payload
      });
    },
    update: (payload: FormData) => {
      return Api.post({
        url: CONST.typhoidVaccination.update,
        data: payload
      });
    },
    delete: (payload: ITyphoidVaccinationEditDTO) => {
      return Api.post({
        url: CONST.typhoidVaccination.delete,
        data: payload
      });
    },
    getById: (id: number) => {
      return Api.get({
        url: CONST.typhoidVaccination.getById(id)
      });
    }
  },
  employeeMedicalCheckup: {
    /**
     * Employee medical checkup master list
     */
    searchAll: (payload: IEmployeeMedicalCheckupSearchForm) => {
      return Api.post({
        url: CONST.employeeMedicalCheckup.search,
        data: payload
      });
    },
    deleteV2: (payload: EmployeeMedicalCheckup) => {
      return Api.post({
        url: CONST.employeeMedicalCheckup.delete,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<EmployeeMedicalCheckup[]>(
        addHeader({
          method: "GET",
          url: "/employee-medical-checkup"
        })
      );
    },
    getByEmployeeId: (employeeId: number) => {
      return axiosClient<EmployeeMedicalCheckupDetail[]>(
        addHeader({
          method: "GET",
          url: `/employee-medical-checkup/employee/${employeeId}`
        })
      );
    }
  },

  /**
   * medical checkup types
   */
  medicalCheckupTypesV2: {
    getAll: (payload: MedicalCheckupTypeSearchForm) => {
      return Api.post({
        url: CONST.medicalCheckupTypes.search,
        data: payload
      });
    },
    getById: (id: number) => {
      return Api.get({
        url: CONST.medicalCheckupTypes.detail(id)
      });
    },
    create: (checkup: MedicalCheckupType) => {
      return Api.post({
        url: CONST.medicalCheckupTypes.create,
        data: checkup
      });
    },
    update: (checkup: MedicalCheckupType) => {
      return Api.post({
        url: CONST.medicalCheckupTypes.update,
        data: checkup
      });
    }
  },

  medicalCheckupTypes: {
    getAll: () => {
      return axiosClient<MedicalCheckupType[]>(
        addHeader({
          method: "GET",
          url: "/employee-medical-checkup-type"
        })
      );
    },
    getById: (id: number) => {
      return axiosClient<MedicalCheckupType>(
        addHeader({
          method: "GET",
          url: `/employee-medical-checkup-type/${id}`
        })
      );
    },
    create: (checkup: MedicalCheckupType) => {
      return axiosClient<MedicalCheckupType>(
        addHeader({
          method: "POST",
          url: "/employee-medical-checkup-type/create",
          headers: { "Content-Type": "application/json" },
          data: checkup
        })
      );
    },
    update: (checkup: MedicalCheckupType) => {
      return axiosClient<MedicalCheckupType>(
        addHeader({
          method: "POST",
          url: "/employee-medical-checkup-type/update",
          headers: { "Content-Type": "application/json" },
          data: checkup
        })
      );
    }
  },

  trainingType: {
    getAll: () => {
      return axiosClient<BaseTrainingType[]>(
        addHeader({
          method: "GET",
          url: "/training-type"
        })
      );
    },
    getById: (trainingTypeId: number) => {
      return axiosClient<BaseTrainingType>(
        addHeader({
          method: "GET",
          url: "/training-type/" + trainingTypeId
        })
      );
    },
    create: (trainingType: BaseTrainingType) => {
      return axiosClient<BaseTrainingType>(
        addHeader({
          method: "POST",
          url: "/training-type/create",
          headers: { "Content-Type": "application/json" },
          data: trainingType
        })
      );
    },
    update: (trainingType: BaseTrainingType) => {
      return axiosClient<BaseTrainingType>(
        addHeader({
          method: "POST",
          url: "/training-type/update",
          headers: { "Content-Type": "application/json" },
          data: trainingType
        })
      );
    }
  },
  trainingProvider: {
    searchAll: (payload: ITrainingProviderSearchForm) => {
      return Api.post({
        url: CONST.trainingProvider.search,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<TrainingProvider[]>(
        addHeader({
          method: "GET",
          url: "/training-provider"
        })
      );
    },
    getById: (providerId: number) => {
      return axiosClient<TrainingProvider>(
        addHeader({
          method: "GET",
          url: "/training-provider/" + providerId
        })
      );
    },
    createV2: (payload: TrainingProvider) => {
      return Api.post({
        url: CONST.trainingProvider.create,
        data: payload
      });
    },
    updateV2: (payload: TrainingProvider) => {
      return Api.post({
        url: CONST.trainingProvider.update,
        data: payload
      });
    },
    deleteV2: (payload: TrainingProvider) => {
      return Api.deleteData({
        url: CONST.trainingProvider.delete,
        data: payload
      });
    }
  },
  training: {
    searchAll: (payload: ITrainingSearchForm) => {
      return Api.post({
        url: CONST.training.search,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<TrainingBase[]>(
        addHeader({
          method: "GET",
          url: "/training"
        })
      );
    },
    getById: (trainingId: number) => {
      return axiosClient<TrainingDetail>(
        addHeader({
          method: "GET",
          url: "/training/" + trainingId
        })
      );
    },
    getByIdV2: (trainingId: number) => {
      return Api.get({
        url: CONST.training.detail(trainingId)
      });
    },
    getByEmployeeId: (employeeId: number) => {
      return axiosClient<TrainingBase[]>(
        addHeader({
          method: "GET",
          url: "/training/employee/" + employeeId
        })
      );
    },
    createV2: (payload: TrainingDetail) => {
      return Api.post({
        url: CONST.training.create,
        data: payload
      });
    },
    updateV2: (payload: TrainingDetail) => {
      return Api.post({
        url: CONST.training.update,
        data: payload
      });
    },
    deleteV2: (payload: TrainingDetail) => {
      return Api.deleteData({
        url: CONST.training.delete,
        data: payload
      });
    },
    getFileUrl: (trainingId: number) => {
      return Api.get({
        url: CONST.training.preview(trainingId)
      });
    }
  },
  trainingCourse: {
    searchAll: (payload: ITrainingCourseSearchForm) => {
      return Api.post({
        url: CONST.trainingCourse.search,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<TrainingCourseBase[]>(
        addHeader({
          method: "GET",
          url: "/training-course/all"
        })
      );
    },
    getById: (trainingId: number) => {
      return Api.get({
        url: CONST.trainingCourse.detail(trainingId)
      });
    },
    createV2: (payload: TrainingCourseDetail) => {
      return Api.post({
        url: CONST.trainingCourse.create,
        data: payload
      });
    },
    updateV2: (payload: TrainingCourseDetail) => {
      return Api.post({
        url: CONST.trainingCourse.update,
        data: payload
      });
    },
    deleteV2: (payload: TrainingCourseDetail) => {
      return Api.deleteData({
        url: CONST.trainingCourse.delete,
        data: payload
      });
    },
    getAllCourseType: () => {
      return axiosClient<CourseType[]>(
        addHeader({
          method: "GET",
          url: "/training-course/all-course-type"
        })
      );
    },
    getAllCourseStatus: () => {
      return axiosClient<TrainingCourseStatus[]>(
        addHeader({
          method: "GET",
          url: "/training-course/all-course-status"
        })
      );
    }
  },
  trainingCourseAssignment: {
    getAll: () => {
      return Api.get({
        url: CONST.trainingCourseAssignment.getAll
      });
    },
    searchAll: (courseId: number) => {
      return Api.get({
        url: CONST.trainingCourseAssignment.search(courseId)
      });
    },
    getById: (trainingId: number) => {
      return Api.get({
        url: CONST.trainingCourseAssignment.detail(trainingId)
      });
    },
    getAllRepeatTypes: () => {
      return axiosClient<TrainingCourseStatus[]>(
        addHeader({
          method: "GET",
          url: "/training-course/all-repeat-type"
        })
      );
    },
    update: (payload: CourseAssignment) => {
      return Api.post({
        url: CONST.trainingCourseAssignment.update,
        data: payload
      });
    },
    delete: (payload: CourseAssignment) => {
      return Api.post({
        url: CONST.trainingCourseAssignment.delete,
        data: payload
      });
    }
  },
  vaccinationType: {
    getAll: () => {
      return axiosClient<VaccinationType[]>(
        addHeader({
          method: "GET",
          url: "/employee-vacc-type"
        })
      );
    },
    getById: (vaccId: number) => {
      return axiosClient<VaccinationType>(
        addHeader({
          method: "GET",
          url: "/employee-vacc-type/" + vaccId
        })
      );
    },
    create: (vaccination: VaccinationType) => {
      return axiosClient<VaccinationType>(
        addHeader({
          method: "POST",
          url: "/employee-vacc-type/create",
          headers: { "Content-Type": "application/json" },
          data: vaccination
        })
      );
    },
    update: (vaccination: VaccinationType) => {
      return axiosClient<VaccinationType>(
        addHeader({
          method: "POST",
          url: "/employee-vacc-type/update",
          headers: { "Content-Type": "application/json" },
          data: vaccination
        })
      );
    }
  },
  user: {
    getAllV2: () => {
      return Api.get({
        url: CONST.user.getAllV2
      });
    },
    getViewSettingTableByListName: (listName: string) => {
      return Api.get({
        url: CONST.user.getViewSettingTableByListName(listName)
      });
    },
    updateViewSettingTable: (payload: any) => {
      return Api.post({
        url: CONST.user.updateViewSettingTable,
        data: payload
      });
    },
    getUsers: (payload = {}) => {
      return Api.post({
        url: CONST.user.search,
        data: payload
      });
    },
    deactivate: (payload = {}) => {
      return Api.post({
        url: CONST.user.deactivate,
        data: payload
      });
    },
    resendInvitation: (payload = {}) => {
      return Api.post({
        url: CONST.user.resendInvitation,
        data: payload
      });
    },
    getAllUserStatus: () => {
      return Api.get({
        url: CONST.user.getAllUserStatus
      });
    },
    getUsersByRole: (payload = {}) => {
      return Api.post({
        url: CONST.user.searchuserbyrole,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<UserDTO[]>(
        addHeader({
          method: "GET",
          url: "/user/all"
        })
      );
    },
    searchAll: (payload: IUserSearchForm) => {
      return Api.post({
        url: CONST.user.search,
        data: payload
      });
    },
    getAllByOrganizationId: (id: number) => {
      return axiosClient<UserDTO[]>(
        addHeader({
          method: "GET",
          url: `/user/all/${id}`
        })
      );
    },
    getByUserId: (id: number) => {
      return axiosClient<UserBase>(
        addHeader({
          method: "GET",
          url: `/user/${id}`
        })
      );
    },
    create: (user: UserDTO) => {
      return axiosClient<UserDTO>(
        addHeader({
          method: "POST",
          url: "/user/create",
          data: user
        })
      );
    },
    createV2: (payload: UserBase) => {
      return Api.post({
        url: CONST.user.create,
        data: payload
      });
    },
    update: (user: UserDTO) => {
      return axiosClient<UserDTO>(
        addHeader({
          method: "POST",
          url: "/user/update",
          data: user
        })
      );
    },
    updateV2: (payload: UserBase) => {
      return Api.post({
        url: CONST.user.update,
        data: payload
      });
    },
    deleteV2: (payload: UserBase) => {
      return Api.deleteData({
        url: CONST.user.delete,
        data: payload
      });
    },
    resetpasswordV2: (payload: IResetPasswordBase) => {
      return Api.post({
        url: CONST.user.resetpassword,
        data: payload
      });
    },
    uploadProfileImage: (payload: File) => {
      return Api.post({
        url: CONST.user.uploadProfileImage,
        data: payload
      });
    },
    searchUserActionLog: (payload: ISearchUserLogRequest) => {
      return Api.post({
        url: CONST.user.searchUserActionLog,
        data: payload
      });
    }
  },
  userPermission: {
    getAll: (userId: number) => {
      return Api.get({
        url: CONST.userPermission.getAllByUserId(userId)
      });
    }
  },
  userRole: {
    getAllByUserId: (id: number) => {
      return Api.get({
        url: CONST.userRole.getRoleByUserId(id)
      });
    }
  },
  rawMaterial: {
    certificate: {
      searchAssociated: (payload: ISearchAssociatedRawMaterialPayload) => {
        return Api.post({
          url: CONST.rawMaterial.certificate.searchAssociated,
          data: payload
        });
      }
    },
    searchAssociatedProduct: (payload: ISearchAssociatedProduct) => {
      return Api.post({
        url: CONST.rawMaterial.certificate.associatedProduct,
        data: payload
      });
    },
    searchAll: (payload: IRawMaterialCertMgmt) => {
      return Api.post({
        url: CONST.rawMaterial.certificate.searchAll,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<RawMaterialBase[]>(
        addHeader({
          method: "GET",
          url: "/raw-material/all"
        })
      );
    },
    getAllV2: (payload: RawMaterialMasterListRequestDTO) => {
      return Api.post({
        url: CONST.rawMaterial.rawMaterialList.search,
        data: payload
      });
    },
    getAllV2A: (payload: IRawMaterialSearchForm) => {
      return Api.post({
        url: CONST.rawMaterial.rawMaterialList.search,
        data: payload
      });
    },
    getBySupplierId: (supplierId: number) => {
      return axiosClient<RawMaterialBase[]>(
        addHeader({
          method: "GET",
          url: `/raw-material/all/supplier/${supplierId}`
        })
      );
    },
    getByMaterialId: (materialId: number) => {
      return axiosClient<RawMaterialEditDTO>(
        addHeader({
          method: "GET",
          url: `/raw-material/${materialId}`
        })
      );
    },
    getByMaterialIdV2: (materialId: number) => {
      //RawMaterialEditDTO
      return Api.get({
        url: CONST.rawMaterial.rawMaterialList.getByMaterialIdV2(materialId)
      });
    },
    getOrigins: () => {
      return axiosClient<RawMaterialOrigin[]>(
        addHeader({
          method: "GET",
          url: "/raw-material/origin"
        })
      );
    },
    getOriginsV2: () => {
      //RawMaterialOrigin[]
      return Api.get({
        url: CONST.rawMaterial.rawMaterialList.getOrigins
      });
    },
    getLinkage: (rawMaterialId: number) => {
      return axiosClient<boolean>(
        addHeader({
          method: "GET",
          url: `/raw-material/delete/${rawMaterialId}/linked`
        })
      );
    },
    create: (rawMaterial: FormData) => {
      return axiosClient<RawMaterialEditDTO>(
        addHeader({
          method: "POST",
          url: "/raw-material/create",
          data: rawMaterial
        })
      );
    },
    createV2: (payload: FormData) => {
      //RawMaterialEditDTO
      return Api.post({
        url: CONST.rawMaterial.rawMaterialList.create,
        data: payload
      });
    },
    update: (rawMaterial: FormData) => {
      return axiosClient<RawMaterialEditDTO>(
        addHeader({
          method: "POST",
          url: "/raw-material/update",
          data: rawMaterial
        })
      );
    },
    updateV2: (payload: FormData) => {
      //RawMaterialEditDTO
      return Api.post({
        url: CONST.rawMaterial.rawMaterialList.update,
        data: payload
      });
    },
    updateStatus: (rmStatusUpdate: RawMaterialStatusUpdate) => {
      return axiosClient(
        addHeader({
          method: "POST",
          url: "/raw-material/update/status",
          headers: { "Content-Type": "application/json" },
          data: rmStatusUpdate
        })
      );
    },
    updateStatusV2: (payload: RawMaterialStatusUpdate) => {
      return Api.post({
        url: CONST.rawMaterial.rawMaterialList.updateSatus,
        data: payload
      });
    },
    downloadRawMaterialFile: (fileId: number) => {
      return axiosClient<File>(
        addHeader({
          method: "GET",
          url: `/raw-material/file/${fileId}/download`
        })
      );
    },
    getStatus: () => {
      return axiosClient<RawMaterialStatus[]>(
        addHeader({
          method: "GET",
          url: "/raw-material/status/all"
        })
      );
    },
    getStatusV2: () => {
      return Api.get({
        url: CONST.rawMaterial.rawMaterialStatus.search
      });
    },
    getStatusById: (statusId: number) => {
      return axiosClient<RawMaterialStatus>(
        addHeader({
          method: "GET",
          url: `/raw-material/status/${statusId}`
        })
      );
    },
    delete: (rmId: number, orgId: number) => {
      return axiosClient<boolean>(
        addHeader({
          method: "DELETE",
          url: `/raw-material/delete/${rmId}/${orgId}`
        })
      );
    },
    deleteV2: (rmId: number, orgId: number) => {
      return Api.deleteData({
        url: CONST.rawMaterial.rawMaterialList.delete(rmId, orgId)
      });
    },
    getStatusHistory: (payload: any) => {
      return Api.post({
        url: CONST.rawMaterial.rawMaterialList.getStatusHistory,
        data: payload
      });
    },
    getGroupsV2: (body: SearchProductGroupBodyDTO) => {
      return Api.post({
        url: CONST.rawMaterial.rawMaterialList.groupSearch,
        data: body
      });
    },
    getGroupAllV2: (body: SearchProductGroupBodyDTO) => {
      return Api.post({
        url: CONST.rawMaterial.rawMaterialList.groupSearchAll,
        data: body
      });
    },
    createGroup: (group: RawMaterialGroup) => {
      return Api.post({
        url: CONST.rawMaterial.rawMaterialList.groupCreate,
        data: group
      });
    },
    updateGroup: (group: RawMaterialGroup) => {
      return Api.post({
        url: CONST.rawMaterial.rawMaterialList.groupUpdate,
        data: group
      });
    },
    deleteGroup: (group: RawMaterialGroup) => {
      return Api.post({
        url: CONST.rawMaterial.rawMaterialList.groupDelete,
        data: group
      });
    }
  },
  rawMaterialMatch: {
    getAll: (payload: RawMaterialMasterListRequestDTO) => {
      return Api.post({
        url: CONST.rawMaterialMatch.search,
        data: payload
      });
    },
    updateMatchRequireStatus: (payload: any) => {
      return Api.post({
        url: CONST.rawMaterialMatch.updateMatchRequireStatus,
        data: payload
      });
    },
    searchMathLog: (payload: any) => {
      return Api.post({
        url: CONST.rawMaterialMatch.searchMathLog,
        data: payload
      });
    },
    confirmMatchLog: (payload: any) => {
      return Api.post({
        url: CONST.rawMaterialMatch.confirmMatchLog,
        data: payload
      });
    }
  },
  rawMaterialMatchingUploadMatch: {
    requestAIMatchByProduct: (payload: any) => {
      return Api.post({
        url: CONST.rawMaterialMatchingUploadMatch.requestAIMatchByProduct,
        data: payload
      });
    },
    resetAIMatchRequest: (payload: any) => {
      return Api.post({
        url: CONST.rawMaterialMatchingUploadMatch.resetAIMatchRequest,
        data: payload
      });
    },
    requestAIMatchList: (payload: any) => {
      return Api.post({
        url: CONST.rawMaterialMatchingUploadMatch.requestAIMatchList,
        data: payload
      });
    },
    getAll: (payload: RawMaterialMasterListRequestDTO) => {
      return Api.post({
        url: CONST.rawMaterialMatchingUploadMatch.search,
        data: payload
      });
    },
    updateMatchRequireStatus: (payload: any) => {
      return Api.post({
        url: CONST.rawMaterialMatchingUploadMatch.updateMatchRequireStatus,
        data: payload
      });
    },
    searchMathLog: (payload: any) => {
      return Api.post({
        url: CONST.rawMaterialMatchingUploadMatch.searchMathLog,
        data: payload
      });
    },
    confirmMatchLog: (payload: any) => {
      return Api.post({
        url: CONST.rawMaterialMatchingUploadMatch.confirmMatchLog,
        data: payload
      });
    },
    ragStatus: () => {
      return Api.get({
        url: CONST.rawMaterialMatchingUploadMatch.ragStatus
      });
    }
  },
  rawMaterialMatchingUpload: {
    // getAll: (payload: SearchRawMaterialMatchingUpload) => {
    //   return Api.post({
    //     url: CONST.rawMaterialMatchingUpload.search,
    //     data: payload
    //   });
    // },
    search: (payload: any) => {
      return Api.post({
        url: CONST.rawMaterialMatchingUpload.search,
        data: payload
      });
    },
    create: (payload: FormData) => {
      return Api.post({
        url: CONST.rawMaterialMatchingUpload.create,
        data: payload
      });
    },
    update: (payload: FormData) => {
      return Api.post({
        url: CONST.rawMaterialMatchingUpload.update,
        data: payload
      });
    },
    getById: (id: number) => {
      return Api.get({
        url: CONST.rawMaterialMatchingUpload.getById(id)
      });
    },
    updateStatus: (payload: any) => {
      return Api.post({
        url: CONST.rawMaterialMatchingUpload.updateStatus,
        data: payload
      });
    },
    getRawMaterialMatchingUploadStatus: () => {
      return Api.get({
        url: CONST.rawMaterialMatchingUpload.getRawMaterialMatchingUploadStatus
      });
    }
  },
  tradingProductRawMaterialMaster: {
    getAllV2: (payload: RawMaterialMasterListRequestDTO) => {
      return Api.post({
        url: CONST.tradingProductRawMaterialMaster.rawMaterialList.search,
        data: payload
      });
    },
    getAllV2A: (payload: IRawMaterialSearchForm) => {
      return Api.post({
        url: CONST.tradingProductRawMaterialMaster.rawMaterialList.search,
        data: payload
      });
    },
    getByMaterialIdV2: (materialId: number) => {
      //RawMaterialEditDTO
      return Api.get({
        url: CONST.tradingProductRawMaterialMaster.rawMaterialList.getByMaterialIdV2(materialId)
      });
    },
    createV2: (payload: FormData) => {
      //RawMaterialEditDTO
      return Api.post({
        url: CONST.tradingProductRawMaterialMaster.rawMaterialList.create,
        data: payload
      });
    },
    updateV2: (payload: FormData) => {
      //RawMaterialEditDTO
      return Api.post({
        url: CONST.tradingProductRawMaterialMaster.rawMaterialList.update,
        data: payload
      });
    },
    updateStatusV2: (payload: RawMaterialStatusUpdate) => {
      return Api.post({
        url: CONST.tradingProductRawMaterialMaster.rawMaterialList.updateSatus,
        data: payload
      });
    },
    deleteV2: (rmId: number) => {
      return Api.deleteData({
        url: CONST.tradingProductRawMaterialMaster.rawMaterialList.delete(rmId)
      });
    }
  },
  rawMaterialPurchase: {
    searchRawMaterialBelongPurchase: (payload: IRawMaterialPurchase) => {
      return Api.post({
        url: CONST.purchasing.rawMaterialPurchase.searchRawMaterialBelongPurchase,
        data: payload
      });
    },
    searchAll: (payload: IRawMaterialPurchase) => {
      return Api.post({
        url: CONST.purchasing.rawMaterialPurchase.searchAll,
        data: payload
      });
    },
    delete: (id: number) => {
      return Api.deleteData({
        url: CONST.purchasing.rawMaterialPurchase.delete(id)
      });
    },
    getAll: () => {
      return axiosClient<RawMaterialPurchaseBase[]>(
        addHeader({
          method: "GET",
          url: "/raw-material-purchase"
        })
      );
    },
    getRawMaterialPurchaseById: (rawMaterialId: number) => {
      return Api.get({
        url: CONST.rawMaterial.rawMaterialPurchase.getRawMaterialPurchaseById(rawMaterialId)
      });
    },
    getById: (purchaseId: number) => {
      return axiosClient<RawMaterialPurchaseDetail>(
        addHeader({
          method: "GET",
          url: `/raw-material-purchase/${purchaseId}`
        })
      );
    },
    getByRawMaterialId: (rawMaterialId: number) => {
      return axiosClient<RawMaterialPurchaseBase[]>(
        addHeader({
          method: "GET",
          url: `/raw-material-purchase/raw-material/${rawMaterialId}`
        })
      );
    },
    getByRawMaterialIdV2: (rawMaterialId: number) => {
      //RawMaterialPurchaseBase[]
      return Api.get({
        url: CONST.rawMaterial.rawMaterialPurchase.getByRawMaterialId(rawMaterialId)
      });
    },
    createRawMaterialPurchase: (purchaseDetail: FormData) => {
      return Api.post({
        url: CONST.rawMaterial.rawMaterialPurchase.createRawMaterialPurchase,
        data: purchaseDetail
      });
    },
    create: (purchaseDetail: FormData) => {
      return axiosClient<RawMaterialPurchaseDetail>(
        addHeader({
          method: "POST",
          url: "/raw-material-purchase/create",
          data: purchaseDetail
        })
      );
    },
    updateRawMaterialPurchase: (purchaseDetail: FormData) => {
      return Api.post({
        url: CONST.rawMaterial.rawMaterialPurchase.updateRawMaterialPurchase,
        data: purchaseDetail
      });
    },
    update: (purchaseDetail: FormData) => {
      return axiosClient<RawMaterialPurchaseDetail>(
        addHeader({
          method: "POST",
          url: "/raw-material-purchase/update",
          data: purchaseDetail
        })
      );
    }
  },
  purchasing: {
    expensesReport: {
      search: (payload: any) => {
        return Api.post({
          url: CONST.purchasing.expensesReport.search,
          data: payload
        });
      },
      export: (payload: any) => {
        return Api.post({
          url: CONST.purchasing.expensesReport.export,
          data: payload
        });
      },
      getOverallExpenses: (payload: any) => {
        return Api.post({
          url: CONST.purchasing.expensesReport.getOverallExpenses,
          data: payload
        });
      }
    },
    expensesMasterList: {
      delete: (id: number, orgId: number) => {
        return Api.deleteData({
          url: CONST.purchasing.expensesMasterList.delete(id, orgId)
        });
      },
      search: (payload: any) => {
        return Api.post({
          url: CONST.purchasing.expensesMasterList.search,
          data: payload
        });
      },
      getExpenseTypes: () => {
        return Api.get({
          url: CONST.purchasing.expensesMasterList.getExpenseTypes
        });
      },
      getMonths: () => {
        return Api.get({
          url: CONST.purchasing.expensesMasterList.getMonths
        });
      },
      getCategories: (expensesTypeId: number) => {
        return Api.get({
          url: CONST.purchasing.expensesMasterList.getCategories(expensesTypeId)
        });
      },
      getById: (id: number) => {
        return Api.get({
          url: CONST.purchasing.expensesMasterList.getById(id)
        });
      },
      update: (payload: FormData) => {
        return Api.post({
          url: CONST.purchasing.expensesMasterList.update,
          data: payload
        });
      },
      create: (payload: FormData) => {
        return Api.post({
          url: CONST.purchasing.expensesMasterList.create,
          data: payload
        });
      },
      getExpensesHistory: (payload: any) => {
        return Api.post({
          url: CONST.purchasing.expensesMasterList.getExpensesHistory,
          data: payload
        });
      }
    }
  },
  product: {
    getStatusHistory: (payload: any) => {
      return Api.post({
        url: CONST.product.getStatusHistory,
        data: payload
      });
    },
    searchV2: (payload: SearchProductBodyDTO) => {
      return Api.post({
        url: CONST.product.search,
        data: payload
      });
    },
    search: (payload: SearchProductMaster) => {
      // return axiosClient<ProductBase[]>({
      //   method: "POST",
      //   url: "/v2/api/product/search",
      //   data: payload
      // });
      return Api.post({
        url: CONST.product.searchProductMaster,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<ProductBase[]>(
        addHeader({
          method: "GET",
          url: "/product/all"
        })
      );
    },
    getById: (productId: number) => {
      return Api.get({
        url: `${CONST.product.basePath}/${productId}`
      });
      // axiosClient<ProductDetail>(
      //   addHeader({
      //     method: "GET",
      //     url: `/product/${productId}`
      //   })
      // );
    },
    getAllStatusV2: () => {
      return Api.get({
        url: CONST.product.statusAll
      });
    },
    getAllStatus: () => {
      return axiosClient<ProductStatus[]>(
        addHeader({
          method: "GET",
          url: "/product/status/all"
        })
      );
    },
    getStatusById: (statusId: number) => {
      return axiosClient<ProductStatus>(
        addHeader({
          method: "GET",
          url: `/product/status/${statusId}`
        })
      );
    },
    getBrandsV2: (body: SearchProductBrandBodyDTO) => {
      return Api.post({
        url: CONST.product.brandSearch,
        data: body
      });
    },
    getBrandAllV2: (body: SearchProductBrandBodyDTO) => {
      return Api.post({
        url: CONST.product.brandSearchAll,
        data: body
      });
    },
    getBrands: () => {
      return axiosClient<ProductBrand[]>(
        addHeader({
          method: "GET",
          url: "/product/brand/all"
        })
      );
    },
    getGroupsV2: (body: SearchProductGroupBodyDTO) => {
      return Api.post({
        url: CONST.product.groupSearch,
        data: body
      });
    },
    getGroupAllV2: (body: SearchProductGroupBodyDTO) => {
      return Api.post({
        url: CONST.product.groupSearchAll,
        data: body
      });
    },
    getGroups: () => {
      return axiosClient<ProductGroup[]>(
        addHeader({
          method: "GET",
          url: "/product/group/all"
        })
      );
    },
    create: (product: FormData) => {
      return Api.post({
        url: `${CONST.product.basePath}/create`,
        data: product
      });
      // axiosClient<ProductDetail>(
      //   addHeader({
      //     method: "POST",
      //     url: "/product/create",
      //     // headers: { "Content-Type": "multipart/form-data" },
      //     data: product
      //   })
      // );
    },
    update: (product: FormData) => {
      return Api.post({
        url: `${CONST.product.basePath}/update`,
        data: product
      });
      // axiosClient<ProductDetail>(
      //   addHeader({
      //     method: "POST",
      //     url: "/product/update",
      //     data: product
      //   })
      // );
    },
    updateStatus: (productStatusUpdate: ProductStatusUpdate) => {
      return axiosClient(
        addHeader({
          method: "POST",
          url: "/product/update/status",
          headers: { "Content-Type": "application/json" },
          data: productStatusUpdate
        })
      );
    },
    createBrand: (brand: ProductBrand) => {
      return axiosClient<ProductBrand>(
        addHeader({
          method: "POST",
          url: "/product/brand/create",
          headers: { "Content-Type": "application/json" },
          data: brand
        })
      );
    },
    createBrandV2: (brand: ProductBrand) => {
      return Api.post({
        url: `${CONST.product.basePath}/brand/create`,
        data: brand
      });
    },
    updateBrand: (brand: ProductBrand) => {
      return Api.post({
        url: `${CONST.product.basePath}/brand/update`,
        data: brand
      });
    },
    deleteBrand: (brand: ProductBrand) => {
      return Api.post({
        url: `${CONST.product.basePath}/brand/delete`,
        data: brand
      });
    },
    createGroup: (group: ProductGroup) => {
      return axiosClient<ProductGroup>(
        addHeader({
          method: "POST",
          url: "/product/group/create",
          headers: { "Content-Type": "application/json" },
          data: group
        })
      );
    },
    createGroupV2: (group: ProductGroup) => {
      return Api.post({
        url: `${CONST.product.basePath}/group/create`,
        data: group
      });
    },
    updateGroup: (group: ProductGroup) => {
      return Api.post({
        url: `${CONST.product.basePath}/group/update`,
        data: group
      });
    },
    deleteGroup: (group: ProductGroup) => {
      return Api.post({
        url: `${CONST.product.basePath}/group/delete`,
        data: group
      });
    },
    delete: (productId: number, orgId: number) => {
      return axiosClient<boolean>(
        addHeader({
          method: "DELETE",
          url: `/product/delete/${productId}/${orgId}`
        })
      );
    },
    getAllRawmaterialByProductId: (productId: number) => {
      return Api.get({
        url: `${CONST.product.getAllRawmaterialByProductId(productId)}`
      });
    }
  },
  productMenu: {
    getStatusHistory: (payload: any) => {
      return Api.post({
        url: CONST.productMenu.getStatusHistory,
        data: payload
      });
    },
    searchV2: (payload: SearchProductBodyDTO) => {
      return Api.post({
        url: CONST.productMenu.search,
        data: payload
      });
    },
    search: (payload: SearchProductMaster) => {
      // return axiosClient<ProductBase[]>({
      //   method: "POST",
      //   url: "/v2/api/product/search",
      //   data: payload
      // });
      return Api.post({
        url: CONST.productMenu.searchProductMaster,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<ProductBase[]>(
        addHeader({
          method: "GET",
          url: "/product-menu/all"
        })
      );
    },
    getById: (productId: number) => {
      return Api.get({
        url: `${CONST.productMenu.basePath}/${productId}`
      });
      // axiosClient<ProductDetail>(
      //   addHeader({
      //     method: "GET",
      //     url: `/product/${productId}`
      //   })
      // );
    },
    getAllStatusV2: () => {
      return Api.get({
        url: CONST.productMenu.statusAll
      });
    },
    getAllStatus: () => {
      return axiosClient<ProductStatus[]>(
        addHeader({
          method: "GET",
          url: "/product-menu/status/all"
        })
      );
    },
    getStatusById: (statusId: number) => {
      return axiosClient<ProductStatus>(
        addHeader({
          method: "GET",
          url: `/product-menu/status/${statusId}`
        })
      );
    },
    getGroupsV2: (body: SearchProductGroupBodyDTO) => {
      return Api.post({
        url: CONST.productMenu.groupSearch,
        data: body
      });
    },
    getGroupAllV2: (body: SearchProductGroupBodyDTO) => {
      return Api.post({
        url: CONST.productMenu.groupSearchAll,
        data: body
      });
    },
    getGroups: () => {
      return axiosClient<ProductGroup[]>(
        addHeader({
          method: "GET",
          url: "/product-menu/group/all"
        })
      );
    },
    create: (product: FormData) => {
      return Api.post({
        url: `${CONST.productMenu.basePath}/create`,
        data: product
      });
      // axiosClient<ProductDetail>(
      //   addHeader({
      //     method: "POST",
      //     url: "/product/create",
      //     // headers: { "Content-Type": "multipart/form-data" },
      //     data: product
      //   })
      // );
    },
    update: (product: FormData) => {
      return Api.post({
        url: `${CONST.productMenu.basePath}/update`,
        data: product
      });
      // axiosClient<ProductDetail>(
      //   addHeader({
      //     method: "POST",
      //     url: "/product/update",
      //     data: product
      //   })
      // );
    },
    updateStatus: (productStatusUpdate: ProductStatusUpdate) => {
      return Api.post({
        url: `${CONST.productMenu.basePath}/update/status`,
        data: productStatusUpdate
      });
      //return axiosClient(
      //  addHeader({
      //    method: "POST",
      //    url: "/product-menu/update/status",
      //    headers: { "Content-Type": "application/json" },
      //    data: productStatusUpdate
      //  })
      //);
    },
    createGroup: (group: ProductGroup) => {
      return axiosClient<ProductGroup>(
        addHeader({
          method: "POST",
          url: "/product-menu/group/create",
          headers: { "Content-Type": "application/json" },
          data: group
        })
      );
    },
    createGroupV2: (group: ProductGroup) => {
      return Api.post({
        url: `${CONST.productMenu.basePath}/group/create`,
        data: group
      });
    },
    updateGroup: (group: ProductGroup) => {
      return Api.post({
        url: `${CONST.productMenu.basePath}/group/update`,
        data: group
      });
    },
    deleteGroup: (group: ProductGroup) => {
      return Api.post({
        url: `${CONST.productMenu.basePath}/group/delete`,
        data: group
      });
    },
    deleteV2: (productId: number, orgId: number) => {
      return Api.deleteData({
        url: CONST.productMenu.delete(productId, orgId)
      });
    },
    delete: (productId: number, orgId: number) => {
      return axiosClient<boolean>(
        addHeader({
          method: "DELETE",
          url: `/product-menu/delete/${productId}/${orgId}`
        })
      );
    }
  },
  productTrading: {
    getProductTradingType: () => {
      return Api.get({
        url: CONST.productTrading.getProductTradingType
      });
    },
    getStatusHistory: (payload: any) => {
      return Api.post({
        url: CONST.productTrading.getStatusHistory,
        data: payload
      });
    },
    searchV2: (payload: SearchProductBodyDTO) => {
      return Api.post({
        url: CONST.productTrading.search,
        data: payload
      });
    },
    search: (payload: SearchProductMaster) => {
      return Api.post({
        url: CONST.productTrading.search,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<ProductBase[]>(
        addHeader({
          method: "GET",
          url: "/product/all"
        })
      );
    },
    getById: (productId: number) => {
      return Api.get({
        url: `${CONST.productTrading.basePath}/${productId}`
      });
      // axiosClient<ProductDetail>(
      //   addHeader({
      //     method: "GET",
      //     url: `/product/${productId}`
      //   })
      // );
    },
    getAllStatusV2: () => {
      return Api.get({
        url: CONST.productTrading.statusAll
      });
    },
    getAllStatus: () => {
      return axiosClient<ProductStatus[]>(
        addHeader({
          method: "GET",
          url: "/product/status/all"
        })
      );
    },
    getStatusById: (statusId: number) => {
      return axiosClient<ProductStatus>(
        addHeader({
          method: "GET",
          url: `/product/status/${statusId}`
        })
      );
    },
    getBrandsV2: (body: SearchProductBrandBodyDTO) => {
      return Api.post({
        url: CONST.productTrading.brandSearch,
        data: body
      });
    },
    getBrandAllV2: (body: SearchProductBrandBodyDTO) => {
      return Api.post({
        url: CONST.productTrading.brandSearchAll,
        data: body
      });
    },
    getBrands: () => {
      return axiosClient<ProductBrand[]>(
        addHeader({
          method: "GET",
          url: "/product/brand/all"
        })
      );
    },
    getGroupsV2: (body: SearchProductGroupBodyDTO) => {
      return Api.post({
        url: CONST.productTrading.groupSearch,
        data: body
      });
    },
    getGroupAllV2: (body: SearchProductGroupBodyDTO) => {
      return Api.post({
        url: CONST.productTrading.groupSearchAll,
        data: body
      });
    },
    getGroups: () => {
      return axiosClient<ProductGroup[]>(
        addHeader({
          method: "GET",
          url: "/product/group/all"
        })
      );
    },
    create: (product: FormData) => {
      return Api.post({
        url: `${CONST.productTrading.basePath}/create`,
        data: product
      });
      // axiosClient<ProductDetail>(
      //   addHeader({
      //     method: "POST",
      //     url: "/product/create",
      //     // headers: { "Content-Type": "multipart/form-data" },
      //     data: product
      //   })
      // );
    },
    update: (product: FormData) => {
      return Api.post({
        url: `${CONST.productTrading.basePath}/update`,
        data: product
      });
      // axiosClient<ProductDetail>(
      //   addHeader({
      //     method: "POST",
      //     url: "/product/update",
      //     data: product
      //   })
      // );
    },
    updateStatus: (productStatusUpdate: ProductStatusUpdate) => {
      return Api.post({
        url: CONST.productTrading.updateStatus,
        data: productStatusUpdate
      });
    },
    createBrand: (brand: ProductBrand) => {
      return axiosClient<ProductBrand>(
        addHeader({
          method: "POST",
          url: "/product/brand/create",
          headers: { "Content-Type": "application/json" },
          data: brand
        })
      );
    },
    createBrandV2: (brand: ProductBrand) => {
      return Api.post({
        url: `${CONST.productTrading.basePath}/brand/create`,
        data: brand
      });
    },
    updateBrand: (brand: ProductBrand) => {
      return Api.post({
        url: `${CONST.productTrading.basePath}/brand/update`,
        data: brand
      });
    },
    deleteBrand: (brand: ProductBrand) => {
      return Api.post({
        url: CONST.productTrading.brandDelete,
        data: brand
      });
    },
    createGroup: (group: ProductGroup) => {
      return axiosClient<ProductGroup>(
        addHeader({
          method: "POST",
          url: "/product/group/create",
          headers: { "Content-Type": "application/json" },
          data: group
        })
      );
    },
    createGroupV2: (group: ProductGroup) => {
      return Api.post({
        url: CONST.productTrading.groupCreate,
        data: group
      });
    },
    updateGroup: (group: ProductGroup) => {
      return Api.post({
        url: CONST.productTrading.groupUpdate,
        data: group
      });
    },
    deleteGroup: (group: ProductGroup) => {
      return Api.post({
        url: CONST.productTrading.groupDelete,
        data: group
      });
    },
    delete: (productId: number, orgId: number) => {
      return axiosClient<boolean>(
        addHeader({
          method: "DELETE",
          url: `/product-trading/delete/${productId}/${orgId}`
        })
      );
    },
    deleteV2: (productId: number, orgId: number) => {
      return Api.deleteData({
        url: CONST.productTrading.delete(productId, orgId)
      });
    },
    getAllRawmaterialByProductId: (productId: number) => {
      return Api.get({
        url: `${CONST.productTrading.getAllRawmaterialByProductId(productId)}`
      });
    }
  },
  certificateUpload: {
    search: (payload: any) => {
      return Api.post({
        url: CONST.certificateUpload.search,
        data: payload
      });
    },
    create: (payload: any) => {
      return Api.post({
        url: CONST.certificateUpload.create,
        data: payload
      });
    },
    update: (payload: any) => {
      return Api.post({
        url: CONST.certificateUpload.update,
        data: payload
      });
    },
    getCertificateUploadStatus: () => {
      return Api.get({
        url: CONST.certificateUpload.getCertificateUploadStatus
      });
    },
    delete: (id: number) => {
      return Api.deleteData({
        url: CONST.certificateUpload.delete(id)
      });
    },
    getById: (id: number) => {
      return Api.get({
        url: CONST.certificateUpload.getById(id)
      });
    },
    getCertificateUploadFileInfo: (id: number) => {
      return Api.get({
        url: CONST.certificateUpload.getCertificateUploadFileInfo(id)
      });
    },
    updateStatus: (payload: any) => {
      return Api.post({
        url: CONST.certificateUpload.updateStatus,
        data: payload
      });
    },
    saveRawMaterial: (payload: any) => {
      return Api.post({
        url: CONST.certificateUpload.saveRawMaterial,
        data: payload
      });
    },
    processRawMaterial: (payload: any) => {
      return Api.post({
        url: CONST.certificateUpload.processRawMaterial,
        data: payload
      });
    }
  },
  certificateMatchingUpload: {
    bulkDeleteByRawMaterial: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.bulkDeleteByRawMaterial,
        data: payload
      });
    },
    bulkPushByRawMaterial: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.bulkPushByRawMaterial,
        data: payload
      });
    },
    deleteByRawMaterial: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.deleteByRawMaterial,
        data: payload
      });
    },
    bulkDeleteByCertificate: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.bulkDeleteByCertificate,
        data: payload
      });
    },
    deleteByCertificate: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.deleteByCertificate,
        data: payload
      });
    },
    pushByRawMaterial: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.pushByRawMaterial,
        data: payload
      });
    },
    searchByCertificate: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.searchByCertificate,
        data: payload
      });
    },
    searchByRawMaterial: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.searchByRawMaterial,
        data: payload
      });
    },
    search: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.search,
        data: payload
      });
    },
    create: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.create,
        data: payload
      });
    },
    update: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.update,
        data: payload
      });
    },
    getCertificateMatchingUploadStatus: () => {
      return Api.get({
        url: CONST.certificateMatchingUpload.getCertificateMatchingUploadStatus
      });
    },
    delete: (id: number) => {
      return Api.deleteData({
        url: CONST.certificateMatchingUpload.delete(id)
      });
    },
    getById: (id: number) => {
      return Api.get({
        url: CONST.certificateMatchingUpload.getById(id)
      });
    },
    getCertificateMatchingUploadFileInfo: (id: number) => {
      return Api.get({
        url: CONST.certificateMatchingUpload.getCertificateMatchingUploadFileInfo(id)
      });
    },
    updateStatus: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.updateStatus,
        data: payload
      });
    },
    saveRawMaterial: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.saveRawMaterial,
        data: payload
      });
    },
    processRawMaterial: (payload: any) => {
      return Api.post({
        url: CONST.certificateMatchingUpload.processRawMaterial,
        data: payload
      });
    }
  },
  role: {
    getAll: () => {
      return axiosClient<Role[]>(
        addHeader({
          method: "GET",
          url: "/role/all"
        })
      );
    },
    getByRoleId: (id: number) => {
      return axiosClient<Role>(
        addHeader({
          method: "GET",
          url: `/role/${id}`
        })
      );
    }
  },
  organization: {
    getAllStatus: () => {
      return Api.get({
        url: CONST.organization.getAllStatus
      });
    },
    updateOrganizationPackageAssignMenu: (payload: any) => {
      return Api.post({
        url: CONST.organization.updateOrganizationPackageAssignMenu,
        data: payload
      });
    },
    getOrganizationPackageAssignMenu: (id: number) => {
      return Api.get({
        url: CONST.organization.getOrganizationPackageAssignMenu(id)
      });
    },
    searchAll: (payload: IOrganizationSearchForm) => {
      return Api.post({
        url: CONST.organization.search,
        data: payload
      });
    },
    getAllV2: () => {
      return Api.get({
        url: CONST.organization.getAll,
        data: {}
      });
    },
    getAll: () => {
      return axiosClient<Organization[]>(
        addHeader({
          method: "GET",
          url: "/organization/all"
        })
      );
    },
    getByOrganizationIdV2: (id: number) => {
      return Api.get({
        url: CONST.organization.detail(id)
      });
    },
    getByOrganizationId: (id: number) => {
      return axiosClient<Organization>(
        addHeader({
          method: "GET",
          url: `/organization/${id}`
        })
      );
    },
    getOrganizationNameByOrganizationId: (id: number) => {
      return axiosClient<string>(
        addHeader({
          method: "GET",
          url: `/organization/name/${id}`
        })
      );
    },
    create: (organization: OrganizationEditDTO) => {
      return axiosClient<Organization>(
        addHeader({
          method: "POST",
          url: "/organization/create",
          data: organization
        })
      );
    },
    update: (organization: OrganizationEditDTO) => {
      return axiosClient<Organization>(
        addHeader({
          method: "POST",
          url: "/organization/update",
          data: organization
        })
      );
    },
    createV2: (organization: OrganizationEditDTO) => {
      return Api.post({
        url: CONST.organization.create,
        data: organization
      });
    },
    updateV2: (organization: OrganizationEditDTO) => {
      return Api.post({
        url: CONST.organization.update,
        data: organization
      });
    },
    delete: (organization: Organization) => {
      return axiosClient<Organization>(
        addHeader({
          method: "POST",
          url: "/organization/delete",
          data: organization
        })
      );
    },
    deleteV2: (id: number) => {
      return Api.post({
        url: CONST.organization.delete,
        data: { id }
      });
    },
    switchOrganization: (payload: { organizationId: number }) => {
      return Api.post({
        url: CONST.organization.switchOrganization,
        data: payload
      });
    },
    getOrganizationDetail: (payload: { userId: number }) => {
      return Api.post({
        url: CONST.organization.getOrganizationDetail,
        data: payload
      });
    },
    deleteOrganization: (payload: { userId: number; organizationId: number }) => {
      return Api.post({
        url: CONST.organization.deleteOrganization,
        data: payload
      });
    },
    addOrganization: (payload: { userId: number; organizationId: number }) => {
      return Api.post({
        url: CONST.organization.addOrganization,
        data: payload
      });
    }
  },
  site: {
    searchAll: (payload: ISiteSearchForm) => {
      return Api.post({
        url: CONST.site.search,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<Site[]>(
        addHeader({
          method: "GET",
          url: "/site/all"
        })
      );
    },
    getAllV2: () => {
      return Api.get({
        url: CONST.site.getAll
      });
    },
    getAllByOrganizationIdV2: (payload: SearchSiteRequestDTO) => {
      return Api.post({
        url: CONST.site.search,
        data: payload
      });
    },
    getAllByOrganizationIdV3: (payload: SearchSiteRequestDTO) => {
      return Api.post({
        url: CONST.site.searchall,
        data: payload
      });
    },
    getAllByOrganizationId: (id: number) => {
      return axiosClient<Site[]>(
        addHeader({
          method: "GET",
          url: `/site/all/${id}`
        })
      );
    },
    getBySiteId: (id: number) => {
      return axiosClient<Site>(
        addHeader({
          method: "GET",
          url: `/site/${id}`
        })
      );
    },
    getBySiteIdV2: (id: number) => {
      return Api.get({
        url: CONST.site.detail(id)
      });
    },
    create: (site: SiteEditDTO) => {
      return Api.post({
        url: CONST.site.create,
        data: site
      });
    },
    update: (site: SiteEditDTO) => {
      return Api.post({
        url: CONST.site.update,
        data: site
      });
    },
    delete: (site: Site) => {
      return Api.post({
        url: CONST.site.delete,
        data: site
      });
    },
    deleteV2: (site: Site) => {
      return Api.post({
        url: CONST.site.delete,
        data: site
      });
    }
  },
  siteType: {
    getAll: () => {
      return axiosClient<SiteType[]>(
        addHeader({
          method: "GET",
          url: "/site-type/all"
        })
      );
    },
    getAllV2: () => {
      return Api.get({
        url: CONST.siteType.listAll
      });
    },
    listAll: () => {
      return Api.get({
        url: CONST.siteType.listAll
      });
    },
    getBySiteTypeId: (id: number) => {
      return axiosClient<Site>(
        addHeader({
          method: "GET",
          url: `/site-type/${id}`
        })
      );
    },
    listBySiteTypeId: (id: number) => {
      return Api.get({
        url: CONST.siteType.listBySiteTypeId + `${id}`
      });
    }
  },
  businessType: {
    getAll: () => {
      return axiosClient<BusinessType[]>(
        addHeader({
          method: "GET",
          url: "/business-type/all"
        })
      );
    },
    getAllV2: () => {
      return Api.get({
        url: `${CONST.businessType.basePath}/all`
      });
    },
    getByBusinessTypeId: (id: number) => {
      return axiosClient<BusinessType>(
        addHeader({
          method: "GET",
          url: `/business-type/${id}`
        })
      );
    }
  },
  contactPerson: {
    getAll: () => {
      return axiosClient<ContactPerson[]>(
        addHeader({
          method: "GET",
          url: "/contact-person/all"
        })
      );
    },
    getAllV2: () => {
      return Api.get({
        url: `${CONST.contactPerson.basePath}/all`
      });
    },
    getByContactPersonId: (id: number) => {
      return axiosClient<ContactPerson>(
        addHeader({
          method: "GET",
          url: `/contact-person/${id}`
        })
      );
    },
    getByContactPersonIdV2: (id: number) => {
      return Api.get({
        url: `${CONST.contactPerson.basePath}/${id}`
      });
    }
  },
  userConfirmation: {
    getAll: () => {
      return axiosClient<UserConfirmationDTO[]>(
        addHeader({
          method: "GET",
          url: "/user-confirmation/all"
        })
      );
    },
    getByShortGuid: (shortGuid: string) => {
      return axiosClient<UserConfirmationDTO>(
        addHeader({
          method: "GET",
          url: `/user-confirmation/${shortGuid}`
        })
      );
    },
    getByShortGuidV2: (shortGuid: string) => {
      return Api.getNoWarning({
        url: CONST.userConfirmation.getByShortGuidV2(shortGuid)
      });
    },
    confirm: (userConfirmationRequest: UserConfirmationRequestDTO) => {
      return axiosClient<UserConfirmationDTO>(
        addHeader({
          method: "POST",
          url: "/user-confirmation/confirm",
          data: userConfirmationRequest
        })
      );
    },
    supplierActivationByHash: (payload: any) => {
      return Api.post({
        url: CONST.userConfirmation.supplierActivationByHash,
        data: payload
      });
    },
    supplierInviteConfirm: (payload: any) => {
      return Api.post({
        url: CONST.userConfirmation.supplierInviteConfirm,
        data: payload
      });
    }
  },
  supplier: {
    search: (payload: SearchSupplier) => {
      return Api.post({
        url: CONST.supplier.search,
        data: payload
      });
    },
    searchall: (payload: SearchSupplier) => {
      return Api.post({
        url: CONST.supplier.searchall,
        data: payload
      });
    },
    getAllV2: () => {
      return Api.get({
        url: CONST.supplier.getAllV2
      });
    },
    viewPermissionSupplierSide: (payload: any) => {
      return Api.post({
        url: CONST.supplier.viewPermissionSupplierSide,
        data: payload
      });
    },
    updatePermissionSupplierSide: (payload: any) => {
      return Api.post({
        url: CONST.supplier.updatePermissionSupplierSide,
        data: payload
      });
    },
    searchSupplierProductBuyerSide: (payload: any) => {
      return Api.post({
        url: CONST.supplier.searchSupplierProductBuyerSide,
        data: payload
      });
    },
    searchConnectionRequest: (payload: any) => {
      return Api.post({
        url: CONST.supplier.searchConnectionRequest,
        data: payload
      });
    },
    searchConnectionRequestForSupplier: (payload: any) => {
      return Api.post({
        url: CONST.supplier.searchConnectionRequestForSupplier,
        data: payload
      });
    },
    searchConnectionRequestActivityLog: (payload: any) => {
      return Api.post({
        url: CONST.supplier.searchConnectionRequestActivityLog,
        data: payload
      });
    },
    searchConnectionRequestActivityLogForSupplier: (payload: any) => {
      return Api.post({
        url: CONST.supplier.searchConnectionRequestActivityLogForSupplier,
        data: payload
      });
    },
    buyerSendConnectionRequest: (payload: any) => {
      return Api.post({
        url: CONST.supplier.buyerSendConnectionRequest,
        data: payload
      });
    },
    buyerSendConnectionRequestMulti: (payload: any) => {
      return Api.post({
        url: CONST.supplier.buyerSendConnectionRequestMulti,
        data: payload
      });
    },
    buyerRevertConnectionRequest: (payload: any) => {
      return Api.post({
        url: CONST.supplier.buyerRevertConnectionRequest,
        data: payload
      });
    },
    connectionRequestDetailByHash: (payload: any) => {
      return Api.post({
        url: CONST.supplier.connectionRequestDetailByHash,
        data: payload
      });
    },
    connectionRequestDetailAccepted: (payload: any) => {
      return Api.post({
        url: CONST.supplier.connectionRequestDetailAccepted,
        data: payload
      });
    },
    connectionRequestDetailRejected: (payload: any) => {
      return Api.post({
        url: CONST.supplier.connectionRequestDetailRejected,
        data: payload
      });
    },
    buyerSenConnectionDisconnect: (payload: any) => {
      return Api.post({
        url: CONST.supplier.buyerSenConnectionDisconnect,
        data: payload
      });
    },
    supplierSenConnectionDisconnect: (payload: any) => {
      return Api.post({
        url: CONST.supplier.supplierSenConnectionDisconnect,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<Supplier[]>(
        addHeader({
          method: "GET",
          url: "/supplier/all"
        })
      );
    },
    getAllCategories: () => {
      return axiosClient<SupplierCategory[]>(
        addHeader({
          method: "GET",
          url: "/supplier/category-options"
        })
      );
    },
    getAllStatus: () => {
      return Api.get({
        url: CONST.supplier.supplierStatusAll
      });
    },
    getAllCategoriesV2: () => {
      return Api.get({
        url: CONST.supplier.getAllCategories
      });
    },
    getAllByCategoryId: (categoryId: number) => {
      return axiosClient<Supplier[]>(
        addHeader({
          method: "GET",
          url: `/supplier/all/supplier-category/${categoryId}`
        })
      );
    },
    getAllByCategoryIdV2: (categoryId: number) => {
      //Supplier[]
      return Api.get({
        url: CONST.supplier.getAllByCategoryId(categoryId)
      });
    },
    getAllLogistic: (data: any) => {
      //Supplier[]
      return Api.post({
        url: CONST.supplier.getAllLogistic,
        data
      });
    },
    getByIdV2: (supplierId: number) => {
      return Api.get({
        url: `${CONST.supplier.basePath}/${supplierId}`
      });
    },

    getById: (supplierId: number) => {
      return axiosClient<Supplier>(
        addHeader({
          method: "GET",
          url: `/supplier/${supplierId}`
        })
      );
    },
    getAllByOrganizationId: (id: number) => {
      return axiosClient<Supplier[]>(
        addHeader({
          method: "GET",
          url: `/supplier/all/${id}`
        })
      );
    },
    getSupplierOrganizationBySupplierId: (id: number) => {
      return axiosClient<Organization>(
        addHeader({
          method: "GET",
          url: `/supplier/${id}/supplier-organization`
        })
      );
    },
    createV2: (data: Supplier): Promise<ISupplierBaseResponse<Supplier>> => {
      return Api.post({
        url: `${CONST.supplier.basePath}/create`,
        data
      });
    },
    createWithRiskAssessment: (data: Supplier): Promise<ISupplierBaseResponse<Supplier>> => {
      return Api.post({
        url: `${CONST.supplier.basePath}/create-with-assessment`,
        data
      });
    },
    create: (supplier: Supplier) => {
      return axiosClient<Supplier>(
        addHeader({
          method: "POST",
          url: "/supplier/create",
          headers: { "Content-Type": "application/json" },
          data: supplier
        })
      );
    },
    updateV2: (data: Supplier): Promise<ISupplierBaseResponse<Supplier>> => {
      return Api.post({
        url: `${CONST.supplier.basePath}/update`,
        data
      });
    },
    updateWithRiskAssessment: (data: Supplier): Promise<ISupplierBaseResponse<Supplier>> => {
      return Api.post({
        url: `${CONST.supplier.basePath}/update-with-assessment`,
        data
      });
    },
    update: (supplier: Supplier) => {
      return axiosClient<Supplier>(
        addHeader({
          method: "POST",
          url: "/supplier/update",
          headers: { "Content-Type": "application/json" },
          data: supplier
        })
      );
    },
    delete: (supplier: Supplier) => {
      return Api.post({
        url: `${CONST.supplier.basePath}/delete`,
        data: supplier
      });
    }
  },
  logisticProvider: {
    getAll: () => {
      return axiosClient<LogisticProviderDTO[]>(
        addHeader({
          method: "GET",
          url: "/logistic-provider/all"
        })
      );
    },
    getById: (logisticProviderId: number) => {
      return axiosClient<LogisticProviderDTO>(
        addHeader({
          method: "GET",
          url: `/logistic-provider/${logisticProviderId}`
        })
      );
    },
    create: (logisticProvider: LogisticProviderDTO) => {
      return axiosClient<LogisticProviderDTO>(
        addHeader({
          method: "POST",
          url: "/logistic-provider/create",
          headers: { "Content-Type": "application/json" },
          data: logisticProvider
        })
      );
    },
    update: (logisticProvider: LogisticProviderDTO) => {
      return axiosClient<LogisticProviderDTO>(
        addHeader({
          method: "POST",
          url: "/logistic-provider/update",
          headers: { "Content-Type": "application/json" },
          data: logisticProvider
        })
      );
    }
  },
  manufacturer: {
    search: (payload: SearchManufacturer) => {
      return Api.post({
        url: CONST.manufacturer.searchAll,
        data: payload
      });
    },
    getAll: () => {
      return axiosClient<ManufacturerDTO[]>(
        addHeader({
          method: "GET",
          url: "/manufacturer/all"
        })
      );
    },
    getById: (manufacturerId: number) => {
      return axiosClient<ManufacturerDTO>(
        addHeader({
          method: "GET",
          url: `/manufacturer/${manufacturerId}`
        })
      );
    },
    create: (manufacturer: ManufacturerDTO) => {
      return axiosClient<ManufacturerDTO>(
        addHeader({
          method: "POST",
          url: "/manufacturer/create",
          headers: { "Content-Type": "application/json" },
          data: manufacturer
        })
      );
    },
    update: (manufacturer: ManufacturerDTO) => {
      return axiosClient<ManufacturerDTO>(
        addHeader({
          method: "POST",
          url: "/manufacturer/update",
          headers: { "Content-Type": "application/json" },
          data: manufacturer
        })
      );
    }
  },
  menu: {
    getOrganizationMenu: (id: number) => {
      return axiosClient<MenuDTO[]>(
        addHeader({
          method: "GET",
          url: `/menu/organization/${id}`
        })
      );
    },
    getUserMenu: (devicePlatform: DevicePlatform) => {
      return Api.post({
        url: CONST.menu.search,
        data: devicePlatform
      });
    }
  },
  dashboard: {
    getProduct: (date: string) => {
      return Api.get({
        url: CONST.dashboard.product(date)
      });
    },
    getTemperatureHumidity: () => {
      return Api.get({
        url: CONST.dashboard.temperatureHumidity
      });
    },
    getEmployeeVaccination: (date: string) => {
      return Api.get({
        url: CONST.dashboard.employeeVaccination(date)
      });
    },
    getThypoidVacconation: (date: string) => {
      return Api.get({
        url: CONST.dashboard.thypoidVacconation(date)
      });
    },
    getRawMaterialCertificate: (date: string) => {
      return Api.get({
        url: CONST.dashboard.rawMaterialCertificate(date)
      });
    },
    getUpcomingAuditInternal: () => {
      return Api.get({
        url: CONST.dashboard.upcomingAuditInternal
      });
    },
    getNonConformance: (nonConformanceType: string) => {
      return Api.get({
        url: CONST.dashboard.nonConformance,
        params: {
          nonConformanceType
        }
      });
    },
    getAuditSchedule: (date: string) => {
      return Api.get({
        url: CONST.dashboard.auditSchedule(date)
      });
    },
    getTrainingSchedule: (date: string) => {
      return Api.get({
        url: CONST.dashboard.trainingSchedule(date)
      });
    },
    getAuditsSheduleSupplier: (date: string) => {
      return Api.get({
        url: CONST.dashboard.auditsSheduleSupplier(date)
      });
    },
    getExpiringProductPipeChart: () => {
      return Api.get({
        url: CONST.dashboard.getExpiringProductPipeChart()
      });
    },
    getExpiringRawMaterialCertificatePipeChart: () => {
      return Api.get({
        url: CONST.dashboard.getExpiringRawMaterialCertificatePipeChart()
      });
    },
    getTypoidVaccinationPipeChart: () => {
      return Api.get({
        url: CONST.dashboard.getTypoidVaccinationPipeChart()
      });
    },
    getIOTCriticalStatusPipeChart: () => {
      return Api.get({
        url: CONST.dashboard.getIOTCriticalStatusPipeChart()
      });
    },
    getWidgetData: () => {
      return Api.get({
        url: CONST.dashboard.getWidgetData()
      });
    },
    updateWidgetData: (payload: WidgetData) => {
      return Api.post({
        url: CONST.dashboard.updateWidgetData,
        data: payload
      });
    },
    widgetBulkData: (payload: WidgetData[]) => {
      return Api.post({
        url: CONST.dashboard.widgetBulkData,
        data: payload
      });
    },
    getCertificateConnectionRequest: () => {
      return Api.get({
        url: CONST.dashboard.getCertificateConnectionRequest()
      });
    },
    getComplianceScoreTable: (siteId: number) => {
      return Api.get({
        url: CONST.dashboard.getComplianceScoreTable(siteId)
      });
    },
    getComplianceScore: () => {
      return Api.get({
        url: CONST.dashboard.getComplianceScore()
      });
    },
    getExpiringTradingProductCertificatePipeChart: () => {
      return Api.get({
        url: CONST.dashboard.getExpiringTradingProductCertificatePipeChart()
      });
    },
    getStatusRawMaterialPipeChart: () => {
      return Api.get({
        url: CONST.dashboard.getStatusRawMaterialPipeChart()
      });
    },
    getStatusProductPipeChart: () => {
      return Api.get({
        url: CONST.dashboard.getStatusProductPipeChart()
      });
    },
    getStatusTradingProductPipeChart: () => {
      return Api.get({
        url: CONST.dashboard.getStatusTradingProductPipeChart()
      });
    }
  },
  country: {
    searchCountry: (payload: ICountrySearchForm) => {
      return Api.post({
        url: CONST.country.searchCountry,
        data: payload
      });
    },
    searchCountryV2: () => {
      return Api.get({
        url: CONST.country.searchCountryV2
      });
    },
    searchState: (payload: IStateSearchForm) => {
      return Api.post({
        url: CONST.country.searchState,
        data: payload
      });
    },
    searchStatePagination: (payload: IStateSearchForm) => {
      return Api.post({
        url: CONST.country.searchStatePagination,
        data: payload
      });
    },
    getById: (id: number) => {
      return Api.get({
        url: CONST.country.getById(id)
      });
    },
    create: (payload: ICountryDetail | Partial<ICountryDetail>) => {
      return Api.post({
        url: CONST.country.create,
        data: payload
      });
    },
    update: (payload: ICountryDetail | Partial<ICountryDetail>) => {
      return Api.post({
        url: CONST.country.update,
        data: payload
      });
    },
    getStateById: (id: number) => {
      return Api.get({
        url: CONST.country.stateGetById(id)
      });
    },
    createState: (payload: IStateCreatePayload) => {
      return Api.post({
        url: CONST.country.stateCreate,
        data: payload
      });
    },
    updateState: (payload: IStateUpdatePayload) => {
      return Api.post({
        url: CONST.country.stateUpdate,
        data: payload
      });
    },
    deleteCountry: (payload: ICountryDeletePayload) => {
      return Api.post({
        url: CONST.country.deleteCountry,
        data: payload
      });
    },
    deleteState: (payload: IStateDeletePayload) => {
      return Api.post({
        url: CONST.country.deleteState,
        data: payload
      });
    },
    getCountryStatusAll: () => {
      return Api.get({
        url: CONST.country.getCountryStatusAll
      });
    }
  },
  blockChain: {
    historyByDocumentId: (id: string) => {
      return Api.get({
        url: CONST.blockChain.historyByDocumentId(id)
      });
    }
  },
  emailHistory: {
    searchAll: (payload: SearchEmailHistoryDTO) => {
      return Api.post({
        url: CONST.emailHistory.search,
        data: payload
      });
    }
  },
  notificationTemplate: {
    searchAll: (payload: SearchNotificationTemplateDTO) => {
      return Api.post({
        url: CONST.notificationTemplate.search,
        data: payload
      });
    },
    getById: (id: number) => {
      return Api.get({
        url: CONST.notificationTemplate.getById(id)
        // data: payload
      });
    },
    create: (payload: NotificationTemplate) => {
      return Api.post({
        url: CONST.notificationTemplate.create,
        data: payload
      });
    },
    update: (payload: NotificationTemplate) => {
      return Api.post({
        url: CONST.notificationTemplate.update,
        data: payload
      });
    }
  },
  temperatureHumidity: {
    searchAll: (payload: ISearchGateWay) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.search,
        data: payload
      });
    },
    searchAllAdmin: (payload: ISearchGateWay) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.searchAdmin,
        data: payload
      });
    },
    searchAllAdminCustomerGateway: (payload: ISearchGateWay) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.searchAdminCustomerGateway,
        data: payload
      });
    },
    searchAllDevice: (payload: SensorListRequestDTO) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.searchDevice,
        data: payload
      });
    },
    searchAdminAllDevice: (payload: SensorListRequestDTO) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.searchAdminDevice,
        data: payload
      });
    },
    searchAllNotification: (payload: NotificationListRequestDTO) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.searchNotification,
        data: payload
      });
    },
    getGatewayById: (id: number) => {
      return Api.get({
        url: CONST.monitoring.temperatureHumidity.searchById(id),
        data: {}
      });
    },
    getAdminGatewayById: (id: number) => {
      return Api.get({
        url: CONST.monitoring.temperatureHumidity.adminSearchById(id),
        data: {}
      });
    },
    getDeviceListByGatewayId: (gatewayId: number) => {
      return Api.get({
        url: CONST.monitoring.temperatureHumidity.searchDeviceByGatewayId(gatewayId),
        data: {}
      });
    },
    getDeviceByDeviceId: (deviceId: number) => {
      return Api.get({
        url: CONST.monitoring.temperatureHumidity.getDeviceByDeviceId(deviceId),
        data: {}
      });
    },
    reloadDeviceById: (deviceId: number) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.reloadDeviceByDeviceId(deviceId),
        data: {}
      });
    },
    reloadAdminDeviceById: (deviceId: number) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.reloadAdminDeviceByDeviceId(deviceId),
        data: {}
      });
    },
    createGateway: (payload: IGatewayPayload) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.createGateway,
        data: payload
      });
    },
    createAdminGateway: (payload: IAdminGatewayPayload) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.createAdminGateway,
        data: payload
      });
    },
    createCustomerGateway: (payload: IAdminGatewayPayload) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.createCustomerGateway,
        data: payload
      });
    },
    updateCustomerGateway: (payload: IAdminGatewayPayload) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.updateCustomerGateway,
        data: payload
      });
    },
    mappingDeviceToOrgId: (payload: IMappingDeviceToOrgId) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.mappingDevice,
        data: payload
      });
    },
    deleteMappingDevice: (payload: IDeleteMappingDeviceToOrgId) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.deleteMappingDevice,
        data: payload
      });
    },
    updateGateway: (payload: IGatewayPayload) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.updateGateway,
        data: payload
      });
    },
    updateAdminGateway: (payload: IGatewayPayload) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.updateAdminGateway,
        data: payload
      });
    },
    updateDevice: (payload: IUpdateDevice) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.updateDevice,
        data: payload
      });
    },
    searchDeviceHistoryV2: (payload: ISearchDeviceHistory) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.searchDeviceHistory,
        data: payload
      });
    },
    createNotification: (payload: IDeviceNotification) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.createNotification,
        data: payload
      });
    },
    updateNotification: (payload: IDeviceNotification) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.updateNotification,
        data: payload
      });
    },
    getNotificationById: (id: number) => {
      return Api.get({
        url: CONST.monitoring.temperatureHumidity.searchNotificationById(id),
        data: {}
      });
    },
    deleteNotification: (payload: IDeviceNotification) => {
      return Api.post({
        url: CONST.monitoring.temperatureHumidity.deleteNotification,
        data: payload
      });
    },
    getGatewayByOrgId: (orgId: number) => {
      return Api.get({
        url: CONST.monitoring.temperatureHumidity.searchGatewayByOrgId(orgId),
        data: {}
      });
    }
  },
  complaint: {
    delete: (payload: any) => {
      return Api.post({
        url: CONST.complaint.delete,
        data: payload
      });
    },
    searchAll: (payload: ISearchComplaint) => {
      return Api.post({
        url: CONST.complaint.searchAll,
        data: payload
      });
    },
    heCreate: (payload: FormData) => {
      return Api.post({
        url: CONST.complaint.heCreate,
        data: payload
      });
    },
    heUpdate: (payload: FormData) => {
      return Api.post({
        url: CONST.complaint.heUpdate,
        data: payload
      });
    },
    heSend: (payload: IComplaintSendDTO) => {
      return Api.post({
        url: CONST.complaint.heSend,
        data: payload
      });
    },
    getAllComplaintStatus: () => {
      return Api.get({
        url: CONST.complaint.statusList
      });
    },
    getComplaintById: (id: number) => {
      return Api.get({
        url: CONST.complaint.getComplaintById(id)
      });
    },
    taResolve: (payload: IComplaintResolveDTO) => {
      return Api.post({
        url: CONST.complaint.taResolve,
        data: payload
      });
    },
    heClose: (payload: IComplaintCloseDTO) => {
      return Api.post({
        url: CONST.complaint.heClose,
        data: payload
      });
    },
    ncSearchAll: (payload: ISearchNonConformance) => {
      return Api.post({
        url: CONST.complaint.ncSearchAll,
        data: payload
      });
    },
    getNonConformanceById: (id: number) => {
      return Api.get({
        url: CONST.complaint.getNonConformanceById(id)
      });
    },
    taNcCreate: (payload: FormData) => {
      return Api.post({
        url: CONST.complaint.taNcCreate,
        data: payload
      });
    },
    auditeeResolve: (payload: FormData) => {
      return Api.post({
        url: CONST.complaint.auditeeResolve,
        data: payload
      });
    },
    auditeeGetNonConformanceByComplaintNcId: (complaintNcId: number) => {
      return Api.get({
        url: CONST.complaint.auditeeGetNonConformanceByComplaintNcId(complaintNcId)
      });
    },
    taReview: (payload: INonConformanceReviewDTO) => {
      return Api.post({
        url: CONST.complaint.taReview,
        data: payload
      });
    },
    taGetNcReviewByComplaintNcId: (complaintNcId: number) => {
      return Api.get({
        url: CONST.complaint.taGetNcReviewByComplaintNcId(complaintNcId)
      });
    },
    downloadByBlobId: (id: number) => {
      return Api.get({
        url: CONST.complaint.download(id)
      });
    },
    getAllNcrStatus: () => {
      return Api.get({
        url: CONST.complaint.getAllNcrStatus
      });
    }
  },
  customerVoice: {
    getSubmissionHistoryById: (id: number) => {
      return Api.get({
        url: CONST.customerVoice.getSubmissionHistoryById(id)
      });
    },
    searchSubmissionHistory: (payload: ISearchSubmissionHistory) => {
      return Api.post({
        url: CONST.customerVoice.searchSubmissionHistory,
        data: payload
      });
    },
    delete: (payload: any) => {
      return Api.postNoWarning({
        url: CONST.customerVoice.delete,
        data: payload
      });
    },
    searchAll: (payload: ISearchComplaint) => {
      return Api.post({
        url: CONST.customerVoice.searchAll,
        data: payload
      });
    },
    searchExport: (payload: any) => {
      return Api.post({
        url: CONST.customerVoice.searchExport,
        data: payload
      });
    },
    heCreate: (payload: FormData) => {
      return Api.post({
        url: CONST.customerVoice.heCreate,
        data: payload
      });
    },
    heUpdate: (payload: FormData) => {
      return Api.post({
        url: CONST.customerVoice.heUpdate,
        data: payload
      });
    },
    heSend: (payload: IComplaintSendDTO) => {
      return Api.post({
        url: CONST.customerVoice.heSend,
        data: payload
      });
    },
    getCustomerVoiceById: (id: number) => {
      return Api.get({
        url: CONST.customerVoice.getCustomerVoiceById(id)
      });
    },
    taResolve: (payload: IComplaintResolveDTO) => {
      return Api.post({
        url: CONST.customerVoice.taResolve,
        data: payload
      });
    },
    heClose: (payload: IComplaintCloseDTO) => {
      return Api.post({
        url: CONST.customerVoice.heClose,
        data: payload
      });
    },
    heHold: (payload: any) => {
      return Api.post({
        url: CONST.customerVoice.heHold,
        data: payload
      });
    },
    heReject: (payload: any) => {
      return Api.post({
        url: CONST.customerVoice.heReject,
        data: payload
      });
    },
    customerVoiceTypeAll: () => {
      return Api.get({
        url: CONST.customerVoice.customerVoiceTypeAll
      });
    },
    customerVoiceAboutAll: () => {
      return Api.get({
        url: CONST.customerVoice.customerVoiceAboutAll
      });
    },
    heVerify: (payload: any) => {
      return Api.post({
        url: CONST.customerVoice.heVerify,
        data: payload
      });
    }
  },
  roleAccess: {
    getAllRoles: () => {
      return Api.get({
        url: CONST.roleAccess.getAllRoles
      });
    },
    getMenuByRole: (params: number) => {
      return Api.get({
        url: CONST.roleAccess.getMenuByRoles(params)
      });
    },
    postRoleAccess: (payload: any) => {
      return Api.post({
        url: CONST.roleAccess.postRoleAccess,
        data: payload
      });
    }
  },
  searchGlobal: {
    getAuditReportInternal: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.auditReportInternal,
        data: payload
      });
    },
    getAuditReportSupplier: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.auditReportSupplier,
        data: payload
      });
    },
    getRawMaterialMasterList: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.rawMaterialMasterList,
        data: payload
      });
    },
    getProductMasterList: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.productMasterList,
        data: payload
      });
    },
    getRawMaterialCertificate: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.rawMaterialCertificate,
        data: payload
      });
    },
    getProductCertificate: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.productCertificate,
        data: payload
      });
    },
    getSupplier: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.supplier,
        data: payload
      });
    },
    getEmployee: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.employee,
        data: payload
      });
    },
    getSite: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.site,
        data: payload
      });
    },
    getNonConformance: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.nonConformance,
        data: payload
      });
    },
    getCustomerComplaint: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.customerComplaint,
        data: payload
      });
    },
    getDocumentCompliance: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.documentCompliance,
        data: payload
      });
    },
    getMonitoringCompliance: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.monitoringCompliance,
        data: payload
      });
    },
    getAuditTemplate: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.auditTemplate,
        data: payload
      });
    },
    getOutgoingRequest: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.outgoingCertRequest,
        data: payload
      });
    },
    // HR module starts here
    getDepartment: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.department,
        data: payload
      });
    },
    getDesignation: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.designation,
        data: payload
      });
    },
    getEmployeeGroup: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.employeeGroup,
        data: payload
      });
    },
    getTraining: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.training,
        data: payload
      });
    },
    getTrainingProvider: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.trainingProvider,
        data: payload
      });
    },
    getTyphoidVaccination: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.typhoidVaccination,
        data: payload
      });
    },
    // HR module ends here
    getUserManagement: (payload: ISearchGlobalPayload) => {
      return Api.post({
        url: CONST.searchGlobal.userManagement,
        data: payload
      });
    }
  },
  getSysCommonVersion: () => {
    return Api.get({
      url: CONST.sysVersion.sysCommonVersion
    });
  },
  iotNotificationSetting: {
    getNotificationDetail: () => {
      return Api.get({
        url: CONST.iotNotificationSetting.getNotificationDetail
      });
    },
    updateNotificationSetting: (payload: INotificationSettingDetail) => {
      return Api.post({
        url: CONST.iotNotificationSetting.updateNotificationSetting,
        data: payload
      });
    },
    getNotificationSettingHistory: (
      payload: INotificationSettingDetail
    ): Promise<IListResponse<INotificationSettingHistory[]>> => {
      return Api.post({
        url: CONST.iotNotificationSetting.getNotificationSettingHistory,
        data: payload
      });
    }
  },
  report: {
    searchSupplierRiskScore: (payload: any) => {
      return Api.post({
        url: CONST.report.searchSupplierRiskScore,
        data: payload
      });
    }
  },
  adminSettings: {
    getOrganizationPackageDetail: (organizationId: number) => {
      return Api.get({
        url: CONST.adminSettings.getOrganizationPackageDetail(organizationId)
      });
    },
    getAdminSettings: (organizationId: number) => {
      return Api.get({
        url: CONST.adminSettings.getAdminSettings(organizationId)
      });
    },
    updateAdminSettings: (payload: IAdminSettings) => {
      return Api.post({
        url: CONST.adminSettings.updateAdminSettings,
        data: payload
      });
    },
    industryCertificationName: {
      search: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.industryCertificationName.search,
          data: payload
        });
      },
      getById: (id: number) => {
        return Api.get({
          url: CONST.adminSettings.industryCertificationName.getById(id)
        });
      },
      update: (payload: FormData) => {
        return Api.post({
          url: CONST.adminSettings.industryCertificationName.update,
          data: payload
        });
      },
      delete: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.industryCertificationName.delete,
          data: payload
        });
      },
      create: (payload: FormData) => {
        return Api.post({
          url: CONST.adminSettings.industryCertificationName.create,
          data: payload
        });
      }
    },
    industryCertificationBody: {
      search: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.industryCertificationBody.search,
          data: payload
        });
      },
      getById: (id: number) => {
        return Api.get({
          url: CONST.adminSettings.industryCertificationBody.getById(id)
        });
      },
      update: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.industryCertificationBody.update,
          data: payload
        });
      },
      delete: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.industryCertificationBody.delete,
          data: payload
        });
      },
      create: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.industryCertificationBody.create,
          data: payload
        });
      }
    },
    pestControlMethod: {
      search: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.pestControlMethod.search,
          data: payload
        });
      },
      getById: (id: number) => {
        return Api.get({
          url: CONST.adminSettings.pestControlMethod.getById(id)
        });
      },
      create: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.pestControlMethod.create,
          data: payload
        });
      },
      update: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.pestControlMethod.update,
          data: payload
        });
      },
      delete: (id: number) => {
        return Api.deleteData({
          url: CONST.adminSettings.pestControlMethod.delete(id)
        });
      },
      getAllStatus: () => {
        return Api.get({
          url: CONST.adminSettings.pestControlMethod.getAllStatus
        });
      },
      getAllMethodUnitCommon: () => {
        return Api.get({
          url: CONST.adminSettings.pestControlMethod.getAllMethodUnitCommon
        });
      }
    },
    pestControlSiteArea: {
      search: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.pestControlSiteArea.search,
          data: payload
        });
      },
      getById: (id: number) => {
        return Api.get({
          url: CONST.adminSettings.pestControlSiteArea.getById(id)
        });
      },
      create: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.pestControlSiteArea.create,
          data: payload
        });
      },
      update: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.pestControlSiteArea.update,
          data: payload
        });
      },
      delete: (id: number) => {
        return Api.deleteData({
          url: CONST.adminSettings.pestControlSiteArea.delete(id)
        });
      },
      getAllStatus: () => {
        return Api.get({
          url: CONST.adminSettings.pestControlSiteArea.getAllStatus
        });
      },
      getAllSites: () => {
        return Api.get({
          url: CONST.adminSettings.pestControlSiteArea.getAllSites
        });
      }
    },
    pestControlSiteAreaStation: {
      search: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.pestControlSiteAreaStation.search,
          data: payload
        });
      },
      getById: (id: number) => {
        return Api.get({
          url: CONST.adminSettings.pestControlSiteAreaStation.getById(id)
        });
      },
      create: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.pestControlSiteAreaStation.create,
          data: payload
        });
      },
      update: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.pestControlSiteAreaStation.update,
          data: payload
        });
      },
      delete: (id: number) => {
        return Api.deleteData({
          url: CONST.adminSettings.pestControlSiteAreaStation.delete(id)
        });
      },
      getAllStatus: () => {
        return Api.get({
          url: CONST.adminSettings.pestControlSiteAreaStation.getAllStatus
        });
      }
    },
    notificationSetting: {
      getNotificationSetting: (organizationId: number) => {
        return Api.get({
          url: CONST.adminSettings.notificationSetting.getNotificationSetting(organizationId)
        });
      },
      updateNotificationSetting: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.notificationSetting.updateNotificationSetting,
          data: payload
        });
      }
    },
    autoCertificateRequestSetting: {
      getAutoCertificateRequestSetting: (organizationId: number) => {
        return Api.get({
          url: CONST.adminSettings.autoCertificateRequestSetting.getAutoCertificateRequestSetting(
            organizationId
          )
        });
      },
      updateAutoCertificateRequestSetting: (
        payload: OrganizationAutoCertificateRequestUpdatePayload
      ) => {
        return Api.post({
          url: CONST.adminSettings.autoCertificateRequestSetting
            .updateAutoCertificateRequestSetting,
          data: payload
        });
      },
      searchSupplier: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.autoCertificateRequestSetting.searchSupplier,
          data: payload
        });
      },
      addSupplierList: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.autoCertificateRequestSetting.addSupplierList,
          data: payload
        });
      },
      deleteSupplier: (payload: any) => {
        return Api.post({
          url: CONST.adminSettings.autoCertificateRequestSetting.deleteSupplier,
          data: payload
        });
      }
    }
  },
  siteLocation: {
    searchAll: (payload: {
      siteId?: number | null;
      siteLocationName?: string;
      search?: string;
      sortColumName?: string;
      sortDirection?: string;
    }) => {
      return Api.post({
        url: CONST.siteLocation.searchAll,
        data: payload
      });
    },
    create: (payload: {
      organizationId: number;
      siteId?: number | null;
      name: string;
      description?: string;
    }) => {
      return Api.post({
        url: CONST.siteLocation.create,
        data: payload
      });
    },
    update: (payload: {
      id: number;
      organizationId: number;
      siteId?: number | null;
      name: string;
      description?: string;
    }) => {
      return Api.post({
        url: CONST.siteLocation.update,
        data: payload
      });
    },
    delete: (payload: { id: number; organizationId: number }) => {
      return Api.post({
        url: CONST.siteLocation.delete,
        data: payload
      });
    }
  },
  pestControl: {
    search: (payload: import("../models/PestControl/interface").IPestControlScheduleSearchForm) => {
      return Api.post({
        url: CONST.pestControl.search,
        data: payload
      });
    },
    getById: (id: number) => {
      return Api.get({
        url: CONST.pestControl.getById(id)
      });
    },
    searchStations: (payload: import("../models/PestControl/interface").IPestControlStationSearchForm) => {
      return Api.post({
        url: CONST.pestControl.searchStations,
        data: payload
      });
    },
    searchAreaStations: (
      payload: import("../models/PestControl/interface").IPestControlAreaStationSearchForm
    ) => {
      return Api.post({
        url: CONST.pestControl.searchAreaStations,
        data: payload
      });
    },
    create: (payload: import("../models/PestControl/interface").PestControlSavePayload) => {
      return Api.post({
        url: CONST.pestControl.create,
        data: payload
      });
    },
    update: (payload: import("../models/PestControl/interface").PestControlSavePayload) => {
      return Api.post({
        url: CONST.pestControl.update,
        data: payload
      });
    },
    clone: (payload: import("../models/PestControl/interface").PestControlClonePayload) => {
      return Api.post({
        url: CONST.pestControl.clone,
        data: payload
      });
    },
    delete: (payload: import("../models/PestControl/interface").PestControlDeletePayload) => {
      return Api.post({
        url: CONST.pestControl.delete,
        data: payload
      });
    },
    deleteStation: (
      payload: import("../models/PestControl/interface").PestControlDeleteStationPayload
    ) => {
      return Api.post({
        url: CONST.pestControl.deleteStation,
        data: payload
      });
    },
    addStations: (payload: import("../models/PestControl/interface").PestControlAddStationsPayload) => {
      return Api.post({
        url: CONST.pestControl.addStations,
        data: payload
      });
    },
    getAllStatus: () => {
      return Api.get({
        url: CONST.pestControl.statusAll
      });
    },
    getAllStatusMaster: () => {
      return Api.get({
        url: CONST.pestControl.statusMasterAll
      });
    },
    getAllMethod: () => {
      return Api.get({
        url: CONST.pestControl.methodAll
      });
    },
    getAllRecurrence: () => {
      return Api.get({
        url: CONST.pestControl.recurrenceAll
      });
    },
    getAllSiteArea: () => {
      return Api.get({
        url: CONST.pestControl.siteAreaAll
      });
    }
  },
  pestControlInspection: {
    search: (
      payload: import("../models/PestControl/interface").IPestControlInspectionSearchForm
    ) => {
      return Api.post({
        url: CONST.pestControlInspection.search,
        data: payload
      });
    },
    getById: (id: number) => {
      return Api.get({
        url: CONST.pestControlInspection.getById(id)
      });
    },
    searchStations: (
      payload: import("../models/PestControl/interface").IPestControlInspectionStationSearchForm
    ) => {
      return Api.post({
        url: CONST.pestControlInspection.searchStations,
        data: payload
      });
    },
    create: (formData: FormData) => {
      return Api.post({
        url: CONST.pestControlInspection.create,
        data: formData
      });
    },
    update: (formData: FormData) => {
      return Api.post({
        url: CONST.pestControlInspection.update,
        data: formData
      });
    },
    uploadSignature: (inspectionId: number, formData: FormData) => {
      return Api.post({
        url: CONST.pestControlInspection.uploadSignature(inspectionId),
        data: formData
      });
    },
    updateStatusComplete: (
      payload: import("../models/PestControl/interface").PestControlInspectionUpdateStatusCompletePayload
    ) => {
      return Api.post({
        url: CONST.pestControlInspection.updateStatusComplete,
        data: payload
      });
    },
    updateStatusVerify: (
      payload: import("../models/PestControl/interface").PestControlInspectionUpdateStatusVerifyPayload
    ) => {
      return Api.post({
        url: CONST.pestControlInspection.updateStatusVerify,
        data: payload
      });
    },
    addStations: (
      payload: import("../models/PestControl/interface").PestControlInspectionAddStationsPayload
    ) => {
      return Api.post({
        url: CONST.pestControlInspection.addStations,
        data: payload
      });
    },
    deleteStation: (
      payload: import("../models/PestControl/interface").PestControlInspectionDeleteStationPayload
    ) => {
      return Api.post({
        url: CONST.pestControlInspection.deleteStation,
        data: payload
      });
    },
    finishStation: (
      payload: import("../models/PestControl/interface").PestControlInspectionFinishStationPayload
    ) => {
      return Api.post({
        url: CONST.pestControlInspection.finishStation,
        data: payload
      });
    },
    editStation: (
      payload: import("../models/PestControl/interface").PestControlInspectionEditStationPayload
    ) => {
      return Api.post({
        url: CONST.pestControlInspection.editStation,
        data: payload
      });
    },
    delete: (
      payload: import("../models/PestControl/interface").PestControlInspectionDeletePayload
    ) => {
      return Api.post({
        url: CONST.pestControlInspection.delete,
        data: payload
      });
    },
    clone: (
      payload: import("../models/PestControl/interface").PestControlInspectionClonePayload
    ) => {
      return Api.post({
        url: CONST.pestControlInspection.clone,
        data: payload
      });
    },
    getAllStatus: () => {
      return Api.get({
        url: CONST.pestControlInspection.statusAll
      });
    },
    getAllMethod: () => {
      return Api.get({
        url: CONST.pestControlInspection.methodAll
      });
    },
    searchYearlySummary: (
      payload: import("../models/PestControl/interface").IPestControlYearlySummarySearchForm
    ) => {
      return Api.post({
        url: CONST.pestControlInspection.searchYearlySummary,
        data: payload
      });
    }
  },
  oeeDevice: {
    saveLayout: (payload: {
      organizationId: number;
      layoutJson: string;
      siteId?: number | null;
      siteLocationId?: number | null;
    }) => {
      return Api.post({
        url: CONST.oeeDevice.saveLayout,
        data: payload
      });
    },
    getLayout: (organizationId: number, siteId?: number | null, siteLocationId?: number | null) => {
      return Api.get({
        url: CONST.oeeDevice.getLayout(organizationId, siteId, siteLocationId)
      });
    }
  },
  systemConfig: {
    deleteOrganizationPackageAssignMenu: (organizationPackageId: number, menuId: number) => {
      return Api.deleteData({
        url: CONST.systemConfig.deleteOrganizationPackageAssignMenu(organizationPackageId, menuId)
      });
    },
    getOrganizationPackageAssignMenu: (organizationPackageId: number) => {
      return Api.get({
        url: CONST.systemConfig.getOrganizationPackageAssignMenu(organizationPackageId)
      });
    },
    updateOrganizationPackageAssignMenu: (payload: any) => {
      return Api.post({
        url: CONST.systemConfig.updateOrganizationPackageAssignMenu,
        data: payload
      });
    },
    getAllOrganizationPackages: () => {
      return Api.get({
        url: CONST.systemConfig.getAllOrganizationPackages
      });
    },
    searchOrganizationPackage: (payload: any) => {
      return Api.post({
        url: CONST.systemConfig.searchOrganizationPackage,
        data: payload
      });
    },
    createOrganizationPackage: (payload: any) => {
      return Api.post({
        url: CONST.systemConfig.createOrganizationPackage,
        data: payload
      });
    },
    updateOrganizationPackage: (payload: any) => {
      return Api.post({
        url: CONST.systemConfig.updateOrganizationPackage,
        data: payload
      });
    },
    getOrganizationPackageById: (id: number) => {
      return Api.get({
        url: CONST.systemConfig.getOrganizationPackageById(id)
      });
    },
    deleteOrganizationPackage: (payload: any) => {
      return Api.post({
        url: CONST.systemConfig.deleteOrganizationPackage,
        data: payload
      });
    },
    industryCertificationName: {
      search: (payload: any) => {
        return Api.post({
          url: CONST.systemConfig.industryCertificationName.search,
          data: payload
        });
      },
      getById: (id: number) => {
        return Api.get({
          url: CONST.systemConfig.industryCertificationName.getById(id)
        });
      },
      create: (payload: FormData) => {
        return Api.post({
          url: CONST.systemConfig.industryCertificationName.create,
          data: payload
        });
      },
      update: (payload: FormData) => {
        return Api.post({
          url: CONST.systemConfig.industryCertificationName.update,
          data: payload
        });
      },
      delete: (payload: any) => {
        return Api.post({
          url: CONST.systemConfig.industryCertificationName.delete,
          data: payload
        });
      }
    }
  },

  // ── Fleet (TT19 Cold Truck Monitoring) ──────────────────────────────────────
  fleet: {
    getDevices: () => Api.get({ url: CONST.fleet.devices }),

    getDevicesSummary: (limit = 500) =>
      Api.get({ url: CONST.fleet.devicesSummary, params: { limit } }),

    updateDevice: (
      hardwareId: string,
      payload: {
        label?: string | null;
        device_int_id?: number | null;
        app_id?: string;
        app_key?: string;
        app_secret?: string;
        clear_credentials?: boolean;
      }
    ) =>
      Api.put({
        url: CONST.fleet.devicesUpdate(hardwareId),
        data: payload
      }),

    registerDevice: (
      hardwareId: string,
      activationCode: string,
      label: string,
      appId?: string,
      appKey?: string,
      appSecret?: string
    ) =>
      Api.post({
        url: CONST.fleet.devicesRegister,
        data: {
          hardware_id: hardwareId,
          activation_code: activationCode,
          label,
          app_id: appId,
          app_key: appKey,
          app_secret: appSecret
        }
      }),

    unregisterDevice: (hardwareId: string) =>
      Api.deleteData({ url: CONST.fleet.devicesDelete(hardwareId) }),

    seedDevice: (hardwareId: string, activationCode: string) =>
      Api.post({
        url: CONST.fleet.devicesSeed,
        data: { hardware_id: hardwareId, activation_code: activationCode }
      }),

    getFleetStatus: (params: { warn_seconds?: number; offline_seconds?: number; limit?: number }) =>
      Api.get({ url: CONST.fleet.status, params }),

    getHistoryMeta: (hardwareId: string) =>
      Api.get({ url: CONST.fleet.historyMeta, params: { hardware_id: hardwareId } }),

    getHistoryRange: (hardwareId: string, startUtc: string, endUtc: string, limit = 20000) =>
      Api.get({
        url: CONST.fleet.historyRange,
        params: { hardware_id: hardwareId, start_utc: startUtc, end_utc: endUtc, limit }
      }),

    getHistoryAggregated: (
      hardwareId: string,
      startUtc: string,
      endUtc: string,
      bucketMinutes = 60
    ) =>
      Api.get({
        url: CONST.fleet.historyAggregated,
        params: {
          hardware_id: hardwareId,
          start_utc: startUtc,
          end_utc: endUtc,
          bucket_minutes: bucketMinutes
        }
      }),

    getDeviceSettings: (hardwareId: string) =>
      Api.get({ url: CONST.fleet.deviceSettings, params: { hardware_id: hardwareId } }),

    saveDeviceSettings: (payload: Record<string, unknown>) =>
      Api.post({ url: CONST.fleet.deviceSettingsSave, data: payload }),

    getAlarmLogRecent: (hardwareId: string, since: string, limit = 20) =>
      Api.get({
        url: CONST.fleet.alarmLogRecent,
        params: { hardware_id: hardwareId, since, limit }
      }),

    getAlarmLogByDate: (hardwareId: string, date: string, limit = 2000) =>
      Api.get({
        url: CONST.fleet.alarmLogByDate,
        params: { hardware_id: hardwareId, date, limit }
      }),

    testAlarm: (hardwareId: string) => Api.post({ url: CONST.fleet.alarmLogTest(hardwareId) }),

    getSensorReadingsByDate: (hardwareId: string, date: string, limit = 2000) =>
      Api.get({
        url: CONST.fleet.sensorReadings,
        params: { hardware_id: hardwareId, date, limit }
      }),

    getTrips: (hardwareId: string, limit = 50) =>
      Api.get({ url: CONST.fleet.tripsList, params: { hardware_id: hardwareId, limit } }),

    getTripById: (id: number) => Api.get({ url: CONST.fleet.tripById(id) }),

    openTrip: (hardwareId: string, startTime: string) =>
      Api.post({
        url: CONST.fleet.tripsOpen,
        data: { hardware_id: hardwareId, start_time: startTime }
      }),

    closeTrip: (tripId: number, endTime: string) =>
      Api.post({ url: CONST.fleet.tripsClose(tripId), data: { end_time: endTime } }),

    saveTrip: (payload: Record<string, unknown>) =>
      Api.post({ url: CONST.fleet.tripsSave, data: payload }),

    getLocations: () => Api.get({ url: CONST.fleet.locations }),

    saveLocation: (payload: Record<string, unknown>) =>
      Api.post({ url: CONST.fleet.locationsSave, data: payload }),

    deleteLocation: (id: number) => Api.deleteData({ url: CONST.fleet.locationsDelete(id) }),

    getNavMenus: () => Api.get({ url: CONST.fleet.navMenus }),

    getBatteryForecast: (hardwareId: string, windowHours = 48, thresholdPct = 20) =>
      Api.get({
        url: CONST.fleet.batteryForecast,
        params: { hardware_id: hardwareId, window_hours: windowHours, threshold_pct: thresholdPct }
      }),

    getBreachSummary: (hardwareId: string, days = 30) =>
      Api.get({
        url: CONST.fleet.breachSummary,
        params: { hardware_id: hardwareId, days }
      }),

    downloadTripReport: (tripId: number) =>
      Api.get({ url: `/api/fleet/trips/${tripId}/report.pdf`, responseType: "blob" })
  }
};

/**
 * Custom error response
 *
 * @example
 * ```
 * {
 *   "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
 *   "title": "One or more validation errors occurred.",
 *   "status": 400,
 *   "traceId": "00-b58127ea331eef01aa573deee6a7def0-61347d987ec85965-00",
 *   "errors": {
 *       "InvoiceNum": [
 *           "The InvoiceNum field is required."
 *       ],
 *       "InvoiceDate": [
 *           "The InvoiceDate field is required."
 *       ]
 *   }
 * }
 * ```
 */
interface CustomErrorResponse {
  type: string;
  title: string;
  status: number;
  traceId: string;
  errors?: { [key: string]: string[] };
  message?: string;
}

/**
 * Generic function to extract message data from error.
 * Use this in catch (error: unknown) {
    console.error(error);
  } block.
 */
function extractErrorMsg(error: unknown): string {
  // handle errors thrown from api calls
  if (error instanceof AxiosError || isAxiosError(error)) {
    /**
     * Both AxiosError and Error would have message within the error object.
     * Some errors might not be stored in error.response.data.errors,
     * so when that happens, the error should fallback to the general error message.
     */
    const nestedErrors = (error as AxiosError<CustomErrorResponse>).response?.data?.errors ?? null;
    if (nestedErrors) {
      // concatenate all errors from response (if any)
      return Object.values(nestedErrors).flat().join(" ");
    }

    /**
     * Simple error message directly from controller.
     */
    const simpleError = (error as AxiosError<CustomErrorResponse>).response?.data?.message ?? null;
    if (simpleError) {
      return simpleError;
    }
  }

  // handle any non-api (runtime) errors
  if (error instanceof Error) {
    return error.message;
  }

  // fallback error message
  return JSON.stringify(error);
}

export { baseURL, extractErrorMsg };
export default api;
