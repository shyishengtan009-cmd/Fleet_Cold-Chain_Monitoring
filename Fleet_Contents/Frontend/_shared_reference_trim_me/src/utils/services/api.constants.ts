const CONST = {
  certification: {
    industryCertification: {
      getIndustryCertificationCategory:
        "/v2/api/industry/certification/industry-certification-category/all",
      searchStatusHistory: "/v2/api/industry/certification/status-history",
      renewIndustryCertification: "/v2/api/industry/certification/renew",
      createIndustryCertification: "/v2/api/industry/certification/create",
      updateIndustryCertification: "/v2/api/industry/certification/update",
      getIndustryCertificationById: (id: number) => `/v2/api/industry/certification/${id}`,
      getCertificationName: "/v2/api/industry/certification/industry-certification-name/all",
      getCertificateBody: "/v2/api/industry/certification/industry-certification-body/all",
      searchIndustryCertification: "/v2/api/industry/certification/search",
      deleteIndustryCertification: "/v2/api/industry/certification/delete",
      getAllCertificationStatusV2:
        "/v2/api/industry/certification/industry-certification-status/all"
    }
  },
  organizationRole: {
    delete: (roleId: number, orgId: number) =>
      `/v2/api/organization-role/delete/${roleId}/${orgId}`,
    getAll: "/v2/api/organization-role/role-code/all",
    search: "/v2/api/organization-role/search",
    getById: (id: number) => `/v2/api/organization-role/${id}`,
    create: "/v2/api/organization-role/create",
    update: "/v2/api/organization-role/update"
  },
  auth: {
    logout: "v2/api/auth/logout"
  },
  menu: {
    search: "v2/api/menu/user"
  },
  userPermission: {
    getAllByUserId: (id: number) => `v2/api/user-permission/all/${id}`
  },
  userRole: {
    getRoleByUserId: (id: number) => `v2/api/user-role/all/${id}`
  },
  notification: {
    readByUserId: (id: number) => `v2/api/notification/read/${id}`,
    unreadByUserId: (id: number) => `v2/api/notification/unread/${id}`,
    markAsReadByNotiId: (id: number) => `v2/api/notification/mark-as-read/${id}`,
    markAsReadAllByUserId: (id: number) => `v2/api/notification/mark-as-read-all/${id}`,
    search: "/v2/api/notification/search"
  },
  user: {
    getAllV2: "/v2/api/user/all",
    getViewSettingTableByListName: (listName: string) =>
      `/v2/api/user/search-view-setting?listName=${listName}`,
    updateViewSettingTable: "/v2/api/user/upsert-search-view-setting",
    search: "/v2/api/user/search",
    searchUserActionLog: "/v2/api/user/search/action-log",
    create: "/v2/api/user/create",
    update: "/v2/api/user/update",
    delete: "/v2/api/user/delete",
    searchuserbyrole: "/v2/api/user/search/role-code",
    detail: (id: number) => `/v2/api/user/${id}`,
    resetpassword: "/v2/api/user/resetpassword",
    deleteRequest: "/v2/api/auth/delete-request",
    uploadProfileImage: "/v2/api/user/upload-profile-image",
    deactivate: "/v2/api/user/suspended",
    resendInvitation: "/v2/api/user/resend-invitation",
    getAllUserStatus: "/v2/api/user/status/all"
  },
  site: {
    getAll: "/v2/api/site/all",
    search: "/v2/api/site/search",
    searchall: "/v2/api/site/search-all",
    create: "/v2/api/site/create",
    update: "/v2/api/site/update",
    detail: (id: number) => `/v2/api/site/${id}`,
    delete: "/v2/api/site/delete"
  },
  siteType: {
    listAll: "/v2/api/site-type/all",
    listBySiteTypeId: (id: number) => `/v2/api/site-type/${id}`
  },
  department: {
    departmentBySite: "/v2/api/department/all/site",
    search: "/v2/api/department/search",
    create: "/v2/api/department/create",
    update: "/v2/api/department/update",
    delete: "/v2/api/department/delete",
    detail: (id: number) => `/v2/api/department/${id}`,
    departmentByOrgId: (orgId: number) => `/v2/api/department/all/organization/${orgId}`
  },
  designation: {
    departmentBySite: "/v2/api/designation/all/site",
    search: "/v2/api/designation/search",
    create: "/v2/api/designation/create",
    update: "/v2/api/designation/update",
    delete: "/v2/api/designation/delete",
    detail: (id: number) => `/v2/api/designation/${id}`
  },
  employeegroup: {
    departmentBySite: "/v2/api/employee-group/all/site",
    search: "/v2/api/employee-group/search",
    create: "/v2/api/employee-group/create",
    update: "/v2/api/employee-group/update",
    delete: "/v2/api/employee-group/delete",
    detail: (id: number) => `/v2/api/employee-group/${id}`
  },
  employee: {
    search: "/v2/api/employee/search",
    create: "/v2/api/employee/create",
    update: "/v2/api/employee/update",
    delete: "/v2/api/employee/delete",
    employeeDownload: (id: number) => `/v2/api/employee/${id}/download`,
    employeeDownloadV2: (id: number) => `/v2/api/employee/${id}/download`,
    detail: (id: number) => `/v2/api/employee/${id}`,
    getAllGender: "/v2/api/employee/gender-options",
    getAllReligion: "/v2/api/employee/religion-options",
    getAllNationalityType: "/v2/api/employee/nationality-type-options"
  },
  employeeTraining: {
    search: "/v2/api/employee-training/search",
    create: "/v2/api/employee-training/create",
    update: "/v2/api/employee-training/update",
    detail: (id: number) => `/v2/api/employee-training/${id}`,
    delete: "/v2/api/employee-training/delete",
    download: (id: number) => `/v2/api/employee-training/${id}/download`
  },
  training: {
    search: "/v2/api/training/search",
    create: "/v2/api/training/create",
    update: "/v2/api/training/update",
    delete: "/v2/api/training/delete",
    detail: (id: number) => `/v2/api/training/${id}`,
    download: (id: number) => `/v2/api/training/${id}/download`,
    preview: (id: number) => `/v2/api/training/${id}/preview`
  },
  trainingCourse: {
    search: "/api/training-course/search",
    create: "/api/training-course/create",
    update: "/api/training-course/update",
    delete: "/api/training-course/delete",
    allCourseType: "/api/training-course/all-course-type",
    allCourseStatus: "/api/training-course/all-course-status",
    detail: (id: number) => `/api/training-course/${id}`
  },
  trainingCourseAssignment: {
    getAll: "/api/training-course/assignment/search-all",
    search: (id: number) => `/api/training-course/assignment/search/${id}`,
    update: "/api/training-course/assignment/update",
    delete: "/api/training-course/assignment/delete",
    detail: (id: number) => `/api/training-course/assignment/${id}`
  },
  trainingProvider: {
    search: "/v2/api/training-provider/search",
    create: "/v2/api/training-provider/create",
    update: "/v2/api/training-provider/update",
    delete: "/v2/api/training-provider/delete",
    detail: (id: number) => `/v2/api/training-provider/${id}`
  },
  employeeVaccination: {
    search: "/v2/api/employee-vaccination/search",
    create: "/v2/api/employee-vaccination/create",
    update: "/v2/api/employee-vaccination/update",
    delete: "/v2/api/employee-vaccination/delete",
    employeeVaccinationDownload: (id: number) => `/v2/api/employee-vaccination/${id}/download`,
    detail: (id: number) => `/v2/api/employee-vaccination/${id}`
  },
  typhoidVaccination: {
    search: "/v2/api/typhoid-vaccination/search",
    create: "/v2/api/typhoid-vaccination/create",
    update: "/v2/api/typhoid-vaccination/update",
    delete: "/v2/api/typhoid-vaccination/delete",
    getById: (id: number) => `/v2/api/typhoid-vaccination/${id}`,
    typhoidVaccinationDownload: (id: number) => `/v2/api/typhoid-vaccination/${id}/download`
  },
  employeeMedicalCheckup: {
    search: "/v2/api/employee-medical-checkup/search",
    create: "/v2/api/employee-medical-checkup/create",
    update: "/v2/api/employee-medical-checkup/update",
    delete: "/v2/api/employee-medical-checkup/delete",
    employeeMedicalCheckupDownload: (id: number) =>
      `/v2/api/employee-medical-checkup/${id}/download`,
    detail: (id: number) => `/v2/api/employee-medical-checkup/${id}`
  },
  medicalCheckupTypes: {
    search: "/v2/api/employee-medical-checkup-type/search",
    detail: (id: number) => `/v2/api/employee-medical-checkup-type/${id}`,
    create: "/v2/api/employee-medical-checkup-type/create",
    update: "/v2/api/employee-medical-checkup-type/update",
    delete: "/v2/api/employee-medical-checkup-type/delete"
  },
  auditType: {
    auditTypeAll: "/v2/api/audit/type/all",
    getAllV2: "/v2/api/audit/type/all"
  },
  checklistTemplate: {
    search: "/v2/api/checklist/template/search",
    delete: "/v2/api/checklist/template/delete",
    update: "/v2/api/checklist/template/update",
    updatenew: "/v2/api/checklist/template/updatenew",
    create: "/v2/api/checklist/template/create",
    createnew: "/v2/api/checklist/template/createnew",
    detail: (id: number) => `v2/api/checklist/template/${id}`,
    uploadTemplate: "/v2/api/checklist/template/upload-template",
    downloadTemplate: "/v2/api/checklist/template/download-template"
  },
  checklistTemplateStatus: {
    getAll: "/v2/api/checklist/template/status/all"
  },
  checklistTemplateRevision: {
    getSectionByRevisionId: (id: number) => `/v2/api/checklist/template/revision/${id}/sections`
  },
  checklistResponse: {
    update: "/v2/api/checklist/response/update",
    create: "/v2/api/checklist/response/create",
    updateStatus: "/v2/api/checklist/response/update-status",
    statusHistory: "/v2/api/checklist/response/status-history",
    detail: (id: number) => `v2/api/checklist/response/${id}`,
    score: (id: number) => `v2/api/checklist/response/score/${id}`,
    scoreall: (id: number) => `v2/api/checklist/response/scoreall/${id}`
  },
  supplier: {
    basePath: "/v2/api/supplier",
    search: "/v2/api/supplier/search",
    searchall: "/v2/api/supplier/search-all",
    getAllV2: "/v2/api/supplier/search-all",
    getAllCategories: "/v2/api/supplier/category-options",
    getAllByCategoryId: (categoryId: number) =>
      `/v2/api/supplier/all/supplier-category/${categoryId}`,
    getAllByOrganizationId: (id: number) => `/v2/api/supplier/all/${id}`,
    getAllLogistic: "/v2/api/supplier/search-logistic",
    searchConnectionRequest: "/v2/api/supplier/search-connection-request",
    searchConnectionRequestForSupplier: "/v2/api/supplier/search-connection-request-supplier-side",
    searchConnectionRequestActivityLog: "/v2/api/supplier/search-connection-request-activity-log",
    searchConnectionRequestActivityLogForSupplier:
      "/v2/api/supplier/search-connection-request-activity-log-supplier-side",
    buyerSendConnectionRequest: "/v2/api/supplier/buyer-send-connection-request",
    buyerRevertConnectionRequest: "/v2/api/supplier/buyer-cancel-connection-request",
    connectionRequestDetailByHash: "/v2/api/supplier/connection-request-detail-by-hash",
    connectionRequestDetailAccepted: "/v2/api/supplier/connection-request-detail-accepted",
    connectionRequestDetailRejected: "/v2/api/supplier/connection-request-detail-rejected",
    buyerSenConnectionDisconnect: "/v2/api/supplier/buyer-send-connection-disconnect",
    supplierSenConnectionDisconnect: "/v2/api/supplier/supplier-send-connection-disconnect",
    viewPermissionSupplierSide: "/v2/api/supplier/view-permission-supplier-side",
    updatePermissionSupplierSide: "/v2/api/supplier/update-permission-supplier-side",
    searchSupplierProductBuyerSide: "/v2/api/supplier/search-supplier-product-buyer-side",
    supplierStatusAll: "/v2/api/supplier/status/all",
    buyerSendConnectionRequestMulti: "/v2/api/supplier/buyer-send-connection-request-multi"
  },
  auditSchedule: {
    auditorType: "/v2/api/audit/schedule/audit-auditor-type",
    canDelete: (id: number) => `/v2/api/audit/schedule/candelete/${id}`,
    delete: "/v2/api/audit/schedule/delete",
    file: (id: number) => `/v2/api/audit/schedule/file/${id}`,
    agenda: (id: number) => `/v2/api/audit/schedule/agenda/${id}`,
    auditors: (id: number) => `/v2/api/audit/schedule/auditor/${id}`,
    getAuditScheduleSupplierByAuditScheduleId: (id: number) =>
      `/v2/api/audit/schedule/supplier/${id}`,
    getAuditScheduleInternalByAuditScheduleId: (id: number) =>
      `/v2/api/audit/schedule/internal/${id}`,
    auditScheduleDonload: (id: number) => `/v2/api/audit/schedule/download/file/${id}`,
    internal: {
      search: "/v2/api/audit/schedule/internal/search",
      update: "/v2/api/audit/schedule/internal/update",
      create: "/v2/api/audit/schedule/internal/create",
      detail: (id: number) => `/v2/api/audit/schedule/internal/${id}`,
      auditee: (id: number) => `/v2/api/audit/schedule/internal/auditee/${id}`,
      getCertificationBodyAuditAuditeeInternalById: (id: number) =>
        `/v2/api/audit/schedule/certificate-body/auditee/${id}`,
      department: (id: number) => `/v2/api/audit/schedule/internal/department/${id}`,
      departmentCertificationBodyAudit: (id: number) =>
        `/v2/api/audit/schedule/certificate-body/department/${id}`,
      getAuditeeIdsByAuditScheduleInternalIdV2: (id: number) =>
        `/v2/api/audit/schedule/internal/auditee/${id}`,
      getDepartmentIdsByAuditScheduleInternalIdV2: (id: number) =>
        `/v2/api/audit/schedule/internal/department/${id}`,
      getThirdPartyAuditAuditeeInternalById: (id: number) =>
        `/v2/api/audit/schedule/third-party/auditee/${id}`,
      departmentThirdPartyAudit: (id: number) =>
        `/v2/api/audit/schedule/third-party/department/${id}`
    },
    supplier: {
      search: "/v2/api/audit/schedule/supplier/search",
      update: "/v2/api/audit/schedule/supplier/update",
      create: "/v2/api/audit/schedule/supplier/create",
      detail: (id: number) => `/v2/api/audit/schedule/supplier/${id}`
    },
    certificationBody: {
      search: "/v2/api/audit/schedule/certificate-body/search",
      update: "/v2/api/audit/schedule/certificate-body/update",
      create: "/v2/api/audit/schedule/certificate-body/create",
      detail: (id: number) => `/v2/api/audit/schedule/certificate-body/${id}`,
      getCertificationBodyAuditAuditeeInternalById: (id: number) =>
        `/v2/api/audit/schedule/certificate-body/auditee/${id}`,
      departmentCertificationBodyAudit: (id: number) =>
        `/v2/api/audit/schedule/certificate-body/department/${id}`
    },
    buyerAudit: {
      search: "/v2/api/audit/schedule/buyer-audit/search",
      update: "/v2/api/audit/schedule/buyer-audit/update",
      create: "/v2/api/audit/schedule/buyer-audit/create",
      detail: (id: number) => `/v2/api/audit/schedule/buyer-audit/${id}`,
      departmentBuyerAudit: (id: number) => `/v2/api/audit/schedule/buyer-audit/department/${id}`,
      getBuyerAuditAuditeeById: (id: number) => `/v2/api/audit/schedule/buyer-audit/auditee/${id}`
    }
  },
  certificationBodyAudit: {
    searchCertificationBodyAuditSchedule: "/v2/api/audit/schedule/certificate-body/search",
    searchCertificationBodyAuditReport: "/v2/api/audit/report/certificate-body/search",
    getCertificationBodyAuditScheduleById: (id: number) =>
      `/v2/api/audit/schedule/certificate-body/${id}`,
    getCertificationBodyAuditReportById: (id: number) =>
      `/v2/api/audit/report/certificate-body/${id}`,
    createCertificationBodyAuditSchedule: "/v2/api/audit/schedule/certificate-body/create",
    updateCertificationBodyAuditSchedule: "/v2/api/audit/schedule/certificate-body/update",
    getDepartmentCertificationBodyAudit: (id: number) =>
      `/v2/api/audit/schedule/certificate-body/department/${id}`
  },
  thirdPartyAudit: {
    searchThirdPartyAuditSchedule: "/v2/api/audit/schedule/third-party/search",
    searchThirdPartyAuditReport: "/v2/api/audit/report/third-party/search",
    getThirdPartyAuditScheduleById: (id: number) => `/v2/api/audit/schedule/third-party/${id}`,
    getThirdPartyAuditReportById: (id: number) => `/v2/api/audit/report/third-party/${id}`,
    createThirdPartyAuditSchedule: "/v2/api/audit/schedule/third-party/create",
    updateThirdPartyAuditSchedule: "/v2/api/audit/schedule/third-party/update",
    getDepartmentThirdPartyAudit: (id: number) =>
      `/v2/api/audit/schedule/third-party/department/${id}`
  },
  auditReportStatus: {
    getAll: "/v2/api/audit/report/status/all"
  },
  auditReportChecklistStatus: {
    getAll: "/v2/api/audit/report/checklist/status/all"
  },
  auditReport: {
    getUnsatisfactoryList: "/v2/api/audit/report/unsatisfactory-list",
    auditorUploadSignature: "/v2/api/audit/report/auditor-upload-signature",
    auditeeUploadSignature: "/v2/api/audit/report/auditee-upload-signature",
    auditReportFinal: "/v2/api/audit/report/audit-report-final/print-v3",
    auditReportFinalV2: "/v2/api/audit/report/audit-report-final/print",
    getAllV2: () => "/v2/api/audit/report/search",
    getByAuditReport: (id: number) => `/v2/api/audit/report/${id}`,
    getNonConformancesByAuditReportId: (id: number) => `v2/api/audit/report/${id}/non-conformance`,
    getChecklistResponsesByAuditReportId: (id: number) =>
      `v2/api/audit/report/${id}/checklist-response`,
    getChecklistUploadsByAuditReportId: (id: number) =>
      `v2/api/audit/report/${id}/checklist-upload`,
    DeleteChecklistUploadsByAuditReportId: (id: number) =>
      `v2/api/audit/report/${id}/checklist-upload`,
    updateChecklistResponseStatus: (checklistResponseId: number, statusId: number) =>
      `v2/api/audit/report/checklist-response/${checklistResponseId}/status/update?statusId=${statusId}`,
    updateChecklistUploadStatus: (checklistUploadId: number, statusId: number) =>
      `v2/api/audit/report/checklist-upload/${checklistUploadId}/status/update?statusId=${statusId}`,
    createChecklistUpload: (auditReportId: number) =>
      `v2/api/audit/report/${auditReportId}/checklist-upload/create`,
    auditReportDownload: (id: number) => `/v2/api/audit/report/checklist-upload/${id}/download`,
    getSummaryComplianceChecklistByAuditReportId: (id: number) =>
      `/v2/api/audit/report/checklist-response/clause/${id}`,
    internal: {
      search: "/v2/api/audit/report/internal/search",
      getAllInternalForNonConformance: "/v2/api/audit/report/internal/search-for-non-conformance"
    },
    supplier: {
      search: "/v2/api/audit/report/supplier/search",
      getAllSupplierForNonConformance: "/v2/api/audit/report/supplier/search-for-non-conformance"
    },
    update: "/v2/api/audit/report/update",
    conducted: {
      search: "v2/api/audit/report/audit-report-final/search",
      create: "v2/api/audit/report/audit-report-final/create",
      update: "v2/api/audit/report/audit-report-final/update",
      delete: (id: number) => `v2/api/audit/report/audit-report-final/${id}`,
      detail: (id: number) => `v2/api/audit/report/audit-report-final/${id}`,
      download: (id: number) => `/v2/api/audit/report/audit-report-final/${id}/download`
    },
    buyerAudit: {
      search: "/v2/api/audit/report/buyer-audit/search"
    }
  },
  documentFolder: {
    documentType: {
      documentTypeId: "/v2/api/document-type/all",
      documentStandardTypeId: "/v2/api/document-type/standard/all",
      documentTypeCompliance: "/v2/api/document-type/compliance-document/all",
      documentTypeMonitoring: "/v2/api/document-type/monitoring-log/all"
    },
    createFolder: (folderTypeName: string) => `v2/api/document-folder/${folderTypeName}/create`,
    updateFolder: (folderTypeName: string) => `v2/api/document-folder/${folderTypeName}/update`,
    deleteFolder: (folderTypeName: string) => `v2/api/document-folder/${folderTypeName}/delete`,
    compliance: {
      documentComplianceFile: (id: number) => `/v2/api/document-compliance-file/${id}`,
      documentComplianceFileUploaded: (id: number) =>
        `/v2/api/document-compliance-file/folder/${id}`,
      downloadFileId: (id: number) => `/v2/api/document-compliance-file/${id}/file/download`,
      createUploadFile: "/v2/api/document-compliance-file/create",
      updateUploadFile: "/v2/api/document-compliance-file/update",
      deleteUploadFile: "/v2/api/document-compliance-file/delete",
      searchAll: "v2/api/document-folder/search/compliance",
      getByFolderId: (id: number) => `v2/api/document-folder/compliance/${id}`
    },
    monitoring: {
      documentMonitoringFile: (id: number) => `/v2/api/document-monitoring-file/${id}`,
      documentMonitoringFileUploaded: (id: number) =>
        `/v2/api/document-monitoring-file/folder/${id}`,
      searchAll: "v2/api/document-folder/search/monitoring",
      createFolder: "v2/api/document-folder/monitoring/create",
      getByFolderId: (id: number) => `v2/api/document-folder/monitoring/${id}`,
      updateByFolderId: "v2/api/document-folder/monitoring/update",
      createUploadFile: "/v2/api/document-monitoring-file/create",
      updateUploadFile: "/v2/api/document-monitoring-file/update",
      deleteUploadFile: "/v2/api/document-monitoring-file/delete",
      downloadFileId: (id: number) => `/v2/api/document-monitoring-file/${id}/file/download`
    }
  },
  nonconformance: {
    statusHistory: "/v2/api/non-conformance/status-history",
    search: "v2/api/non-conformance/search",
    delete: "/v2/api/non-conformance/delete",
    create: "/v2/api/non-conformance/create",
    update: "/v2/api/non-conformance/update",
    detail: (id: number) => `/v2/api/non-conformance/${id}`,
    assignList: (id: number) => `/v2/api/non-conformance/assign-list/${id}`,
    dowloadNonConformanceFileByBlobId: (blobId: string) =>
      `/v2/api/non-conformance/file/evidence/${blobId}/download`
  },
  nonconformancetype: {
    searchall: "v2/api/non-conformance/type/all"
  },
  nonConformanceGrading: {
    searchall: "v2/api/non-conformance/grading/all"
  },
  nonConformanceStatus: {
    searchall: "v2/api/non-conformance/status/all"
  },
  manufacturer: {
    searchAll: "v2/api/manufacturer/search"
  },
  certificate: {
    statusHistory: "/v2/api/certificate/status-history",
    create: "/v2/api/certificate/create",
    createV2: "/v2/api/certificate-request/create",
    product: "v2/api/certificate/product/search",
    productSearchAssociated: "/v2/api/certificate/product/search/associated",
    basePath: "v2/api/certificate",
    certificateType: {
      searchAll: "/v2/api/certificate/type/all"
    },
    certificateBody: {
      searchAll: "v2/api/certificate/body/search",
      searchAllV2: "v2/api/certificate/body/search-all",
      getAll: "/v2/api/certificate/body/all",
      update: "/v2/api/certificate/body/update",
      create: "/v2/api/certificate/body/create",
      delete: "/v2/api/certificate/body/delete",
      searchStatus: "/v2/api/certificate/body/status/all",
      detail: (id: number) => `v2/api/certificate/body/${id}`
    },
    rawMaterialCertificate: {
      searchAll: "v2/api/certificate/raw-material/search",
      create: "/v2/api/certificate/create",
      update: "/v2/api/certificate/update",
      delete: (certId: number, orgId: number) => `/v2/api/certificate/delete/${certId}/${orgId}`,
      getById: (id: number) => `/v2/api/certificate/${id}`,
      downloadFileId: (id: number) => `/v2/api/certificate/${id}/file/download`
    },
    outgoingCertificateReq: {
      searchAll: "/v2/api/certificate-request/outgoing/search"
    },
    incomingCertificateReq: {
      searchAll: "/v2/api/certificate-request/incoming/search"
    },
    status: {
      searchAll: "/v2/api/certificate-request/status/all",
      detail: (id: number) => `/v2/api/certificate-request/status/${id}`
    },
    supplier: {
      searchAll: "/v2/api/certificate-request/supplier/search",
      searchCountTab: "/v2/api/certificate-request/supplier/search-count-tab",
      accept: "/v2/api/certificate-request/supplier/accept",
      reject: "/v2/api/certificate-request/supplier/reject",
      upload: "/v2/api/certificate-request/supplier/upload",
      detail: (id: number) => `/v2/api/certificate-request/supplier/${id}`
    },
    admin: {
      searchCountTab: "/v2/api/certificate-request/admin/search-count-tab",
      searchAll: "/v2/api/certificate-request/admin/search",
      confirm: "/v2/api/certificate-request/admin/confirm",
      reject: "/v2/api/certificate-request/admin/reject",
      detail: (id: number) => `/v2/api/certificate-request/admin/${id}`,
      remarkList: (id: number) => `/v2/api/certificate-request/remark-list/${id}`
    },
    downloadFileId: (certificateRequestId: number, certificateId: number) =>
      `/v2/api/certificate-request/${certificateRequestId}/certificate/${certificateId}/file/download`,
    downloadFileIdVer2: (certificateRequestId: number) =>
      `/v2/api/certificate-request/file/upload/${certificateRequestId}`,
    dowloadCertFileByBlobId: (blobId: string) => `/v2/api/certificate/${blobId}/file/download`
  },
  rawMaterial: {
    rawMaterialList: {
      search: "/v2/api/raw-material/search",
      updateSatus: "/v2/api/raw-material/update/status",
      update: "/v2/api/raw-material/update",
      create: "/v2/api/raw-material/create",
      delete: (rmId: number, orgId: number) => `/v2/api/raw-material/delete/${rmId}/${orgId}`,
      getOrigins: "/v2/api/raw-material/origin",
      getByMaterialIdV2: (materialId: number) => `/v2/api/raw-material/${materialId}`,
      getStatusHistory: "/v2/api/raw-material/status-history",
      groupSearch: "v2/api/raw-material/group/search",
      groupSearchAll: "/v2/api/raw-material/group/search-all",
      groupCreate: "/v2/api/raw-material/group/create",
      groupUpdate: "/v2/api/raw-material/group/update",
      groupDelete: "/v2/api/raw-material/group/delete"
    },
    rawMaterialStatus: {
      search: "/v2/api/raw-material/status/all"
    },
    certificate: {
      searchAssociated: "/v2/api/certificate/raw-material/search/associated",
      associatedProduct: "/v2/api/raw-material/search/associated-products",
      searchAll: "/v2/api/certificate/raw-material/search"
    },
    rawMaterialPurchase: {
      getByRawMaterialId: (rawMaterialId: number) =>
        `/v2/api/raw-material-purchase/raw-material/${rawMaterialId}`,
      createRawMaterialPurchase: "/v2/api/raw-material-purchase/create",
      updateRawMaterialPurchase: "/v2/api/raw-material-purchase/update",
      getRawMaterialPurchaseById: (id: number) => `/v2/api/raw-material-purchase/${id}`,
      downloadFileId: (rmpfId: number) => `/v2/api/raw-material-purchase/${rmpfId}/download`
    }
  },
  rawMaterialMatch: {
    search: "/v2/api/raw-material/search-match-require",
    updateMatchRequireStatus: "/v2/api/raw-material/update/match-require-status",
    searchMathLog: "/v2/api/raw-material/search-match-log",
    confirmMatchLog: "/v2/api/raw-material/confirm-match-log"
  },
  rawMaterialMatchingUploadMatch: {
    requestAIMatchByProduct:
      "/v2/api/raw-material-matching-upload/confirm-match-log-by-raw-material",
    resetAIMatchRequest: "/v2/api/raw-material-matching-upload/reload/status",
    requestAIMatchList: "/v2/api/raw-material-matching-upload/update/match-require-status-bulk",
    search: "/v2/api/raw-material-matching-upload/search-match-require",
    updateMatchRequireStatus: "/v2/api/raw-material-matching-upload/update/match-require-status",
    searchMathLog: "/v2/api/raw-material-matching-upload/search-match-log",
    confirmMatchLog: "/v2/api/raw-material-matching-upload/confirm-match-log",
    ragStatus: "v2/api/raw-material-matching-upload/rag-matching-status"
  },
  rawMaterialMatchingUpload: {
    search: "/v2/api/raw-material-matching-upload/search",
    create: "/v2/api/raw-material-matching-upload/create",
    update: "/v2/api/raw-material-matching-upload/update",
    getById: (id: number) => `/v2/api/raw-material-matching-upload/${id}`,
    updateStatus: "/v2/api/raw-material-matching-upload/update/status",
    getRawMaterialMatchingUploadStatus:
      "/v2/api/raw-material-matching-upload/raw-material-matching-upload-status"
  },
  tradingProductRawMaterialMaster: {
    rawMaterialList: {
      search: "/v2/api/product-trading/raw-material/search",
      updateSatus: "/v2/api/product-trading/raw-material/update/status",
      update: "/v2/api/product-trading/raw-material/update",
      create: "/v2/api/product-trading/raw-material/create",
      delete: (rmId: number) => `/v2/api/product-trading/raw-material/delete/${rmId}`,
      getByMaterialIdV2: (materialId: number) =>
        `/v2/api/product-trading/raw-material/${materialId}`
    }
  },
  dashboard: {
    nonConformance: "/v2/api/dashboard/non-conformance",
    upcomingAuditInternal: "v2/api/dashboard/upcoming/audit-internal",
    temperatureHumidity: "/v2/api/dashboard/iot/critical-status",
    product: (date: string) => `v2/api/dashboard/expiring/product/${date}`,
    auditSchedule: (date: string) => `v2/api/dashboard/audit/schedule/${date}`,
    trainingSchedule: (date: string) => `v2/api/dashboard/training/schedule/${date}`,
    thypoidVacconation: (date: string) => `v2/api/dashboard/expiring/thypoid-vaccination/${date}`,
    employeeVaccination: (date: string) => `v2/api/dashboard/expiring/employee-vaccination/${date}`,
    auditsSheduleSupplier: (date: string) => `v2/api/dashboard/audit/schedule/supplier/${date}`,
    rawMaterialCertificate: (date: string) =>
      `v2/api/dashboard/expiring/raw-material-certificate/${date}`,
    getExpiringProductPipeChart: () => "/v2/api/dashboard/expiring/product/pipe-chart",
    getExpiringRawMaterialCertificatePipeChart: () =>
      "/v2/api/dashboard/expiring/raw-material-certificate/pipe-chart",
    getTypoidVaccinationPipeChart: () => "/v2/api/dashboard/expiring/typoid-vaccination/pipe-chart",
    getIOTCriticalStatusPipeChart: () => "/v2/api/dashboard/iot/critical-status/pipe-chart",
    getWidgetData: () => "/v2/api/system-common/dashboard/widgets-all",
    updateWidgetData: "/v2/api/system-common/dashboard/widget-update",
    widgetBulkData: "/v2/api/system-common/dashboard/widget-bulk-update",
    getCertificateConnectionRequest: () => "/v2/api/dashboard/certificate-connection-request",
    getComplianceScoreTable: (siteId: number) =>
      `/v2/api/dashboard/compliance-score-table?siteId=${siteId}`,
    getComplianceScore: () => "/v2/api/dashboard/compliance-score",
    getExpiringTradingProductCertificatePipeChart: () =>
      "/v2/api/dashboard/expiring/product-trading/pipe-chart",
    getStatusRawMaterialPipeChart: () => "/v2/api/dashboard/status/raw-material/pipe-chart",
    getStatusProductPipeChart: () => "/v2/api/dashboard/status/product/pipe-chart",
    getStatusTradingProductPipeChart: () => "/v2/api/dashboard/status/product-trading/pipe-chart"
  },
  product: {
    search: "v2/api/product/search",
    statusAll: "v2/api/product/status/all",
    brandSearch: "v2/api/product/brand/search",
    brandSearchAll: "v2/api/product/brand/search-all",
    groupSearch: "v2/api/product/group/search",
    groupSearchAll: "v2/api/product/group/search-all",
    basePath: "/v2/api/product",
    searchProductMaster: "/v2/api/product/search",
    downloadFileId: (id: number) => `/v2/api/product/file/${id}/download`,
    getAllRawmaterialByProductId: (productId: number) =>
      `/v2/api/product/raw-material/all/${productId}`,
    getStatusHistory: "/v2/api/product/status-history"
  },
  productMenu: {
    search: "v2/api/product-menu/search",
    statusAll: "v2/api/product-menu/status/all",
    groupSearch: "v2/api/product-menu/group/search",
    groupSearchAll: "v2/api/product-menu/group/search-all",
    basePath: "/v2/api/product-menu",
    searchProductMaster: "/v2/api/product-menu/search",
    downloadFileId: (id: number) => `/v2/api/product-menu/file/${id}/download`,
    delete: (productId: number, organizationId: number) =>
      `/v2/api/product-menu/delete/${productId}/${organizationId}`,
    getStatusHistory: "/v2/api/product-menu/status-history"
  },
  productTrading: {
    getProductTradingType: "v2/api/product-trading/search/product-trading-type",
    search: "v2/api/product-trading/search",
    statusAll: "v2/api/product-trading/status/all",
    brandSearch: "v2/api/product-trading/brand/search",
    brandSearchAll: "v2/api/product-trading/brand/search-all",
    groupSearch: "v2/api/product-trading/group/search",
    groupSearchAll: "v2/api/product-trading/group/search-all",
    basePath: "/v2/api/product-trading",
    searchProductMaster: "/v2/api/product-trading/search",
    downloadFileId: (id: number) => `/v2/api/product-trading/file/${id}/download`,
    getAllRawmaterialByProductId: (productId: number) =>
      `/v2/api/product-trading/raw-material/all/${productId}`,
    getStatusHistory: "/v2/api/product-trading/status-history",
    delete: (productId: number, organizationId: number) =>
      `/v2/api/product-trading/delete/${productId}/${organizationId}`,
    updateStatus: "/v2/api/product-trading/update/status",
    brandCreate: "/v2/api/product-trading/brand/create",
    brandUpdate: "/v2/api/product-trading/brand/update",
    brandDelete: "/v2/api/product-trading/brand/delete",
    groupCreate: "/v2/api/product-trading/group/create",
    groupUpdate: "/v2/api/product-trading/group/update",
    groupDelete: "/v2/api/product-trading/group/delete"
  },
  certificateUpload: {
    search: "/v2/api/certificate-upload/search",
    create: "/v2/api/certificate-upload/create",
    update: "/v2/api/certificate-upload/update",
    getById: (id: number) => `/v2/api/certificate-upload/${id}`,
    delete: (id: number) => `/v2/api/certificate-upload/delete/${id}`,
    getCertificateUploadStatus: "/v2/api/certificate-upload/certificate-upload-status",
    getCertificateUploadFileInfo: (id: number) => `/v2/api/certificate-upload/file-detail/${id}`,
    updateStatus: "/v2/api/certificate-upload/update/status",
    saveRawMaterial: "/v2/api/certificate-upload/confirm-upload-file",
    processRawMaterial: "/v2/api/certificate-upload/process-upload-file"
  },
  certificateMatchingUpload: {
    bulkDeleteByCertificate: "/v2/api/certificate-matching-upload/delete-by-certificate-bulk",
    deleteByCertificate: "/v2/api/certificate-matching-upload/delete-by-certificate",
    bulkDeleteByRawMaterial: "/v2/api/certificate-matching-upload/delete-by-raw-material-bulk",
    bulkPushByRawMaterial: "/v2/api/certificate-matching-upload/push-by-raw-material-bulk",
    deleteByRawMaterial: "/v2/api/certificate-matching-upload/delete-by-raw-material",
    pushByRawMaterial: "/v2/api/certificate-matching-upload/push-by-raw-material",
    searchByCertificate: "/v2/api/certificate-matching-upload/search-by-certificate",
    searchByRawMaterial: "/v2/api/certificate-matching-upload/search-by-raw-material",
    search: "/v2/api/certificate-matching-upload/search",
    create: "/v2/api/certificate-matching-upload/create",
    update: "/v2/api/certificate-matching-upload/update",
    getById: (id: number) => `/v2/api/certificate-matching-upload/${id}`,
    delete: (id: number) => `/v2/api/certificate-matching-upload/delete/${id}`,
    getCertificateMatchingUploadStatus:
      "/v2/api/certificate-matching-upload/certificate-upload-status",
    getCertificateMatchingUploadFileInfo: (id: number) =>
      `/v2/api/certificate-matching-upload/file-detail/${id}`,
    updateStatus: "/v2/api/certificate-matching-upload/update/status",
    saveRawMaterial: "/v2/api/certificate-matching-upload/confirm-upload-file",
    processRawMaterial: "/v2/api/certificate-matching-upload/process-upload-file"
  },
  certificateProductTrading: {
    search: "/v2/api/certificate-product-trading/search",
    searchAssociated: "/v2/api/certificate-product-trading/search/associated",
    downloadFileId: (id: number) => `/v2/api/certificate-product-trading/${id}/file/download`,
    create: "/v2/api/certificate-product-trading/create",
    update: "/v2/api/certificate-product-trading/update",
    delete: (certId: number, orgId: number) =>
      `/v2/api/certificate-product-trading/delete/${certId}/${orgId}`,
    exportFile: "/v2/api/certificate-product-trading/search-export",
    getById: (certificateId: number) => `/v2/api/certificate-product-trading/${certificateId}`
  },
  certificateFoodPremise: {
    search: "/v2/api/certificate-food-premise/search",
    searchAssociated: "/v2/api/certificate-food-premise/search/associated",
    searchAssociatedRawMaterial: "/v2/api/certificate-food-premise/search/associated/raw-material",
    downloadFileId: (id: number) => `/v2/api/certificate-food-premise/${id}/file/download`,
    create: "/v2/api/certificate-food-premise/create",
    update: "/v2/api/certificate-food-premise/update",
    delete: (certId: number, orgId: number) =>
      `/v2/api/certificate-food-premise/delete/${certId}/${orgId}`,
    exportFile: "/v2/api/certificate-food-premise/search-export",
    getById: (certificateId: number) => `/v2/api/certificate-food-premise/${certificateId}`
  },
  country: {
    searchCountry: "v2/api/country/search",
    searchCountryV2: "v2/api/country/all",
    searchState: "v2/api/country/state/search",
    searchStatePagination: "v2/api/country/state/search-paging",
    getById: (id: number) => `v2/api/country/${id}`,
    create: "v2/api/country/create",
    update: "v2/api/country/update",
    stateGetById: (id: number) => `v2/api/country/state/${id}`,
    stateCreate: "v2/api/country/state/create",
    stateUpdate: "v2/api/country/state/update",
    deleteCountry: "v2/api/country/delete",
    deleteState: "v2/api/country/delete-state",
    getCountryStatusAll: "v2/api/country/status/all"
  },
  blockChain: {
    historyByDocumentId: (id: string) => `v2/api/blockchain/history/${id}`
  },
  purchasing: {
    rawMaterialPurchase: {
      searchRawMaterialBelongPurchase:
        "/v2/api/raw-material-purchase/search-raw-material-belong-purchase",
      searchAll: "/v2/api/raw-material-purchase/search",
      delete: (id: number) => `/v2/api/raw-material-purchase/delete/${id}`
    },
    expensesMasterList: {
      getExpenseTypes: "/v2/api/expenses/expenses-type/all",
      search: "/v2/api/expenses/search",
      getMonths: "/v2/api/expenses/expenses-month-name/all",
      getCategories: (expensesTypeId: number) =>
        `/v2/api/expenses/expenses-type/${expensesTypeId}/expenses-category/all`,
      getById: (id: number) => `/v2/api/expenses/${id}`,
      delete: (id: number, orgId: number) => `/v2/api/expenses/delete/${id}/${orgId}`,
      update: "/v2/api/expenses/update",
      create: "/v2/api/expenses/create",
      getExpensesHistory: "/v2/api/expenses/expenses-history"
    },
    expensesReport: {
      search: "/v2/api/expenses/expenses-report",
      export: "/v2/api/expenses/expenses-export",
      getOverallExpenses: "/v2/api/expenses/expenses-report-total"
    }
  },
  organization: {
    getAllStatus: "/v2/api/organization/status/all",
    updateOrganizationPackageAssignMenu: "/v2/api/organization/update-menu-list-by-organization-id",
    getOrganizationPackageAssignMenu: (id: number) =>
      `/v2/api/organization/menu-list-by-organization-id/${id}`,
    search: "/v2/api/organization/search",
    getAll: "/v2/api/organization/all",
    create: "/v2/api/organization/create",
    update: "/v2/api/organization/update",
    delete: "/v2/api/organization/delete",
    detail: (id: number) => `v2/api/organization/${id}`,
    switchOrganization: "/api/auth/switch-organization",
    getOrganizationDetail: "/v2/api/user/search/user-multi-organization",
    deleteOrganization: "/v2/api/user/delete/user-multi-organization",
    addOrganization: "/v2/api/user/add/user-multi-organization"
  },
  emailHistory: {
    search: "/v2/api/emailhistory/search"
  },
  notificationTemplate: {
    search: "/v2/api/notification-template/search",
    getById: (id: number) => `/v2/api/notification-template/${id}`,
    create: "/v2/api/notification-template/create",
    update: "/v2/api/notification-template/update"
  },
  resetPassword: {
    verify: (email: string) => `/v2/api/reset-password/verify/${email}`
  },
  businessType: {
    basePath: "/v2/api/business-type"
  },
  contactPerson: {
    basePath: "/v2/api/contact-person"
  },
  monitoring: {
    temperatureHumidity: {
      search: "v2/api/iot/gateway/search",
      searchAdmin: "v2/api/iot/admin/gateway/search",
      searchAdminCustomerGateway: "v2/api/iot/admin/gateway/search-by-organization",
      searchDevice: "/v2/api/iot/device/search",
      searchAdminDevice: "/v2/api/iot/admin/device/search",
      searchNotification: "/v2/api/iot/device/notification/search",
      searchById: (id: number) => `v2/api/iot/gateway/${id}`,
      adminSearchById: (id: number) => `v2/api/iot/admin/gateway/${id}`,

      searchDeviceByGatewayId: (gatewayId: number) =>
        `/v2/api/iot/gateway/device-list/${gatewayId}`,
      getDeviceByDeviceId: (deviceId: number) => `/v2/api/iot/device/${deviceId}`,
      reloadDeviceByDeviceId: (deviceId: number) =>
        `/v2/api/iot/gateway/reload-device-list/${deviceId}`,
      reloadAdminDeviceByDeviceId: (deviceId: number) =>
        `/v2/api/iot/admin/gateway/reload-device-list/${deviceId}`,
      createGateway: "/v2/api/iot/gateway/create",
      createAdminGateway: "v2/api/iot/admin/gateway/create",
      createCustomerGateway: "v2/api/iot/admin/gateway/customer/create",
      updateCustomerGateway: "v2/api/iot/admin/gateway/customer/update",
      mappingDevice: "/v2/api/iot/admin/device/mapping",
      deleteMappingDevice: "/v2/api/iot/admin/device/mapping/delete",
      updateGateway: "/v2/api/iot/gateway/update",
      updateAdminGateway: "/v2/api/iot/admin/gateway/update",
      updateDevice: "/v2/api/iot/device/update",
      searchDeviceHistory: "/v2/api/iot/device/history",
      createNotification: "/v2/api/iot/device/notification/create",
      updateNotification: "/v2/api/iot/device/notification/update",
      deleteNotification: "/v2/api/iot/device/notification/delete",
      searchNotificationById: (id: number) => `/v2/api/iot/device/notification/${id}`,
      searchGatewayByOrgId: (orgId: number) => `/v2/api/iot/admin/gateway/all/${orgId}`
    },
    typhoidVaccination: {
      search: "/v2/api/typhoid-vaccination/monitoring/search"
    }
  },
  complaint: {
    delete: "/v2/api/complaint/halal-executive/delete",
    searchAll: "/v2/api/complaint/search",
    heCreate: "/v2/api/complaint/halal-executive/create",
    heUpdate: "/v2/api/complaint/halal-executive/update",
    getComplaintById: (id: number) => `/v2/api/complaint/${id}`,
    heSend: "/v2/api/complaint/halal-executive/send",
    taResolve: "/v2/api/complaint/technical-auditor/resolve",
    heClose: "/v2/api/complaint/halal-executive/close",
    heHold: "/v2/api/complaint/halal-executive/hold",
    taNcCreate: "/v2/api/complaint/technical-auditor/non-conformance/create",
    taNcDescCreate: "/v2/api/complaint/technical-auditor/non-conformance/description/create",
    auditeeResolve: "/v2/api/complaint/auditee/non-conformance/resolve",
    taReview: "/v2/api/complaint/technical-auditor/non-conformance/review",
    statusList: "/v2/api/complaint/status/all",
    download: (id: number) => `/v2/api/complaint/file/download-blob/${id}`,
    ncSearchAll: "/v2/api/complaint/non-conformance/search",
    getNonConformanceById: (id: number) => `/v2/api/complaint/non-conformance/${id}`,
    auditeeGetNonConformanceByComplaintNcId: (complaintNcId: number) =>
      `/v2/api/complaint/auditee/non-conformance/${complaintNcId}`,
    taGetNcReviewByComplaintNcId: (complaintNcId: number) =>
      `/v2/api/complaint/technical-auditor/non-conformance/review/${complaintNcId}`,
    getAllNcrStatus: "/v2/api/complaint/status/non-conformance-review/all"
  },
  customerVoice: {
    getSubmissionHistoryById: (id: number) => `/v2/api/customer-voice/submission-history/${id}`,
    searchSubmissionHistory: "/v2/api/customer-voice/search-submission-history",
    delete: "/v2/api/customer-voice/halal-executive/delete",
    searchAll: "/v2/api/customer-voice/search",
    searchExport: "/v2/api/customer-voice/search-export",
    heCreate: "/v2/api/customer-voice/halal-executive/create",
    heUpdate: "/v2/api/customer-voice/halal-executive/update",
    getCustomerVoiceById: (id: number) => `/v2/api/customer-voice/${id}`,
    heSend: "/v2/api/customer-voice/halal-executive/send",
    taResolve: "/v2/api/customer-voice/technical-auditor/resolve",
    heClose: "/v2/api/customer-voice/halal-executive/close",
    heHold: "/v2/api/customer-voice/halal-executive/hold",
    heReject: "/v2/api/customer-voice/halal-executive/reject",
    customerVoiceTypeAll: "/v2/api/customer-voice/customer-voice-type/all",
    customerVoiceAboutAll: "/v2/api/customer-voice/customer-voice-about/all",
    heVerify: "/v2/api/customer-voice/halal-executive/verify",
    print: "/v2/api/customer-voice/print"
  },
  roleAccess: {
    getAllRoles: "/v2/api/roles-access/all",
    getMenuByRoles: (id: number) => `v2/api/roles-access/role-by-id/${id}`,
    postRoleAccess: "v2/api/roles-access/role-by-id"
  },
  searchGlobal: {
    auditReportInternal: "v2/api/audit/report/internal/search-global",
    auditReportSupplier: "v2/api/audit/report/supplier/search-global",
    rawMaterialMasterList: "v2/api/raw-material/search-global",
    productMasterList: "v2/api/product/search-global",
    rawMaterialCertificate: "v2/api/certificate/raw-material/search-global",
    productCertificate: "v2/api/certificate/product/search-global",
    supplier: "v2/api/supplier/search-global",
    employee: "v2/api/employee/search-global",
    site: "v2/api/site/search-global",
    nonConformance: "v2/api/non-conformance/search-global",
    customerComplaint: "v2/api/complaint/search-global",
    documentCompliance: "v2/api/document-folder/search/compliance/global",
    monitoringCompliance: "v2/api/document-folder/search/monitoring/global",
    auditTemplate: "v2/api/checklist/template/search-global",
    outgoingCertRequest: "v2/api/certificate-request/admin/search-global",
    department: "v2/api/department/search-global",
    designation: "v2/api/designation/search-global",
    employeeGroup: "v2/api/employee-group/search-global",
    training: "v2/api/training/search-global",
    trainingProvider: "v2/api/training-provider/search-global",
    typhoidVaccination: "v2/api/typhoid-vaccination/search-global",
    userManagement: "v2/api/user/search-global"
  },
  sysVersion: {
    sysCommonVersion: "v2/api/system-common/version"
  },
  userConfirmation: {
    supplierActivationByHash: "/v2/api/supplier/supplier-invite-detail-by-hash",
    supplierInviteConfirm: "/v2/api/supplier/supplier-invite-confirm",
    getByShortGuidV2: (shortGuid: string) => `/v2/api/user-confirmation/${shortGuid}`
  },
  pestControl: {
    search: "/v2/api/pest-control/search",
    statusAll: "/v2/api/pest-control/status/all",
    statusMasterAll: "/v2/api/pest-control/status-master/all",
    methodAll: "/v2/api/pest-control/method/all",
    recurrenceAll: "/v2/api/pest-control/recurrence/all",
    searchStations: "/v2/api/pest-control/search-stations",
    searchAreaStations: "/v2/api/pest-control/search-area-stations",
    create: "/v2/api/pest-control/create",
    update: "/v2/api/pest-control/update",
    clone: "/v2/api/pest-control/clone",
    delete: "/v2/api/pest-control/delete",
    deleteStation: "/v2/api/pest-control/delete-station",
    addStations: "/v2/api/pest-control/add-stations",
    getById: (id: number) => `/v2/api/pest-control/${id}`,
    siteAreaAll: "/v2/api/pest-control/site-area/all"
  },
  pestControlInspection: {
    search: "/v2/api/pest-control/inspection/search",
    getById: (id: number) => `/v2/api/pest-control/inspection/${id}`,
    searchStations: "/v2/api/pest-control/inspection/search-stations",
    create: "/v2/api/pest-control/inspection/create",
    update: "/v2/api/pest-control/inspection/update",
    addStations: "/v2/api/pest-control/inspection/add-stations",
    deleteStation: "/v2/api/pest-control/inspection/delete-station",
    finishStation: "/v2/api/pest-control/inspection/finish-station",
    editStation: "/v2/api/pest-control/inspection/edit-station",
    delete: "/v2/api/pest-control/inspection/delete",
    clone: "/v2/api/pest-control/inspection/clone",
    uploadSignature: (id: number) => `/v2/api/pest-control/inspection/${id}/upload-signature`,
    updateStatusComplete: "/v2/api/pest-control/inspection/update-status-complete",
    updateStatusVerify: "/v2/api/pest-control/inspection/update-status-verify",
    statusAll: "/v2/api/pest-control/status/all",
    methodAll: "/v2/api/pest-control/method/all",
    searchYearlySummary: "/v2/api/pest-control/inspection/search-yearly-summary"
  },
  exportFile: {
    exportInternalAuditSchedule: "/v2/api/audit/schedule/internal/search-export",
    exportSupplierAuditSchedule: "/v2/api/audit/schedule/supplier/search-export",
    exportInternalAuditReport: "/v2/api/audit/report/internal/search-export",
    exportSupplierAuditReport: "/v2/api/audit/report/supplier/search-export",
    exportNonConformance: "/v2/api/non-conformance/search-export",
    exportRawMaterialMasterList: "/v2/api/raw-material/search-export",
    exportProductMasterList: "/v2/api/product/search-export",
    exportProductTradingMasterList: "/v2/api/product-trading/search-export",
    exportRawMaterialCertificate: "/v2/api/certificate/raw-material/search-export",
    exportProductCertificate: "/v2/api/certificate/product/search-export",
    exportFoodPremiseCertificate: "/v2/api/certificate-food-premise/search-export",
    exportMenuMasterList: "/v2/api/product-menu/search-export",
    exportOutgoingCertRequest: "/v2/api/certificate-request/admin/search-export",
    exportSupplier: "/v2/api/supplier/search-export",
    exportRawMaterialPurchase: "/v2/api/raw-material-purchase/search-export",
    exportTyphoidVaccinationMonitoring: "/v2/api/typhoid-vaccination/monitoring/search-export",
    exportTyphoidVaccinationEntry: "/v2/api/typhoid-vaccination/search-export",
    exportCustomerVoice: "/v2/api/customer-voice/search-export",
    exportUserManagement: "/v2/api/user/search-export",
    exportCertificationBodyAuditSchedule: "/v2/api/audit/schedule/certificate-body/search-export",
    exportCertificationBodyAuditReport: "/v2/api/audit/report/certificate-body/search-export",
    exportBuyerAuditSchedule: "/v2/api/audit/schedule/buyer-audit/search-export",
    exportBuyerAuditReport: "/v2/api/audit/report/buyer-audit/search-export",
    exportThirdPartyAuditSchedule: "/v2/api/audit/schedule/third-party/search-export",
    exportThirdPartyAuditReport: "/v2/api/audit/report/third-party/search-export",
    exportSupplierRiskScore: "/v2/api/supplier/report/supplier-assessment-export",
    exportTraining: "/v2/api/training/search-export",
    exportEmployeeTraining: "/v2/api/employee-training/search-export",
    exportRawMaterialMatchingUpload: "/v2/api/raw-material-matching-upload/search-export",
    exportApplication: "/api/application/search-export",
    exportExpensesMasterList: "/v2/api/expenses/search-export",
    exportExpensesReport: "/v2/api/expenses/expenses-export",
    exportEmployee: "/v2/api/employee/search-export",
    exportIndustryCertification: "/v2/api/industry/certification/search-export",
    exportComplianceScoreTable: (siteId: number) =>
      `/v2/api/dashboard/compliance-score-table/export?siteId=${siteId}`,
    exportCertificateRequestFromCustomer: "/v2/api/certificate-request/supplier/search-export",
    exportPestControlSchedule: "/v2/api/pest-control/search-export",
    exportPestControlInspection: "/v2/api/pest-control/inspection/search-export",
    exportPestControlYearlySummary: "/v2/api/pest-control/inspection/search-yearly-summary-export"
  },
  iotNotificationSetting: {
    getNotificationDetail: "/v2/api/iot/notification-config/detail",
    updateNotificationSetting: "/v2/api/iot/notification-config/update",
    getNotificationSettingHistory: "/v2/api/iot/notification-config/history/search"
  },
  report: {
    searchSupplierRiskScore: "/v2/api/supplier/report/supplier-assessment-search"
  },
  adminSettings: {
    getOrganizationPackageDetail: (organizationId: number) =>
      `/v2/api/organization-setting/organization-package-detail/${organizationId}`,
    getAdminSettings: (organizationId: number) => `/v2/api/organization-setting/${organizationId}`,
    updateAdminSettings: "/v2/api/organization-setting/update",
    industryCertificationName: {
      search: "/v2/api/industry/certification-name/search",
      getById: (id: number) => `/v2/api/industry/certification-name/${id}`,
      update: "/v2/api/industry/certification-name/update",
      delete: "/v2/api/industry/certification-name/delete",
      create: "/v2/api/industry/certification-name/create"
    },
    industryCertificationBody: {
      search: "/v2/api/industry/certification-body/search",
      getById: (id: number) => `/v2/api/industry/certification-body/${id}`,
      update: "/v2/api/industry/certification-body/update",
      delete: "/v2/api/industry/certification-body/delete",
      create: "/v2/api/industry/certification-body/create"
    },
    pestControlMethod: {
      search: "/v2/api/pest-control/method/search",
      getById: (id: number) => `/v2/api/pest-control/method/${id}`,
      create: "/v2/api/pest-control/method/create",
      update: "/v2/api/pest-control/method/update",
      delete: (id: number) => `/v2/api/pest-control/method/delete/${id}`,
      getAllStatus: "/v2/api/pest-control/method-status/all",
      getAllMethodUnitCommon: "/v2/api/pest-control/method-unit-common/all"
    },
    pestControlSiteArea: {
      search: "/v2/api/pest-control/site-area/search",
      getById: (id: number) => `/v2/api/pest-control/site-area/${id}`,
      create: "/v2/api/pest-control/site-area/create",
      update: "/v2/api/pest-control/site-area/update",
      delete: (id: number) => `/v2/api/pest-control/site-area/delete/${id}`,
      getAllStatus: "/v2/api/pest-control/site-area-status/all",
      getAllSites: "/v2/api/site/all"
    },
    pestControlSiteAreaStation: {
      search: "/v2/api/pest-control/site-area-station/search",
      getById: (id: number) => `/v2/api/pest-control/site-area-station/${id}`,
      create: "/v2/api/pest-control/site-area-station/create",
      update: "/v2/api/pest-control/site-area-station/update",
      delete: (id: number) => `/v2/api/pest-control/site-area-station/delete/${id}`,
      getAllStatus: "/v2/api/pest-control/site-area-station-status/all"
    },
    notificationSetting: {
      getNotificationSetting: (organizationId: number) =>
        `/v2/api/organization-setting/organization-notification-setting/${organizationId}`,
      updateNotificationSetting:
        "/v2/api/organization-setting/organization-notification-setting/update"
    },
    autoCertificateRequestSetting: {
      getAutoCertificateRequestSetting: (organizationId: number) =>
        `/v2/api/organization-setting-auto-certificate-request/${organizationId}`,
      updateAutoCertificateRequestSetting:
        "/v2/api/organization-setting-auto-certificate-request/update",
      searchSupplier: "/v2/api/organization-setting-auto-certificate-request/search-supplier",
      addSupplierList: "/v2/api/organization-setting-auto-certificate-request/add-supplier-list",
      deleteSupplier: "/v2/api/organization-setting-auto-certificate-request/delete-supplier"
    }
  },
  application: {
    search: "/api/application/search",
    create: "/api/application/create",
    update: "/api/application/update",
    updateStatus: "/api/application/update/status",
    delete: "/api/application/delete",
    detail: (id: number) => `/api/application/${id}`,
    download: (id: number) => `/api/application/${id}/download`,
    downloadFileId: (id: number) => `/api/application/file/${id}/download`,
    check: (appNo: string) => `/api/application/check/${appNo}`
  },
  halalFile: {
    getChecklist: (id: number) => `/api/scheme-checklist/${id}`,
    getFileList: "/api/halal-file/file/list",
    getDocumentList: "/api/halal-file/document/list",
    create: "/api/halal-file/file/create",
    delete: "/api/halal-file/file/delete",
    updateSequence: "/api/halal-file/file/update-sequence",
    downloadFileId: (id: number) => `/api/halal-file/cover/image/${id}/download`,
    detail: (id: number) => `/api/halal-file/${id}`,
    view: (id: number) => `/api/halal-file/view/${id}`,
    getApplication: (id: number) => `/api/halal-file/application/${id}`,
    getCoverLetter: (id: number) => `/api/halal-file/${id}/cover-letter`,
    createCoverLetter: "/api/halal-file/cover/create",
    updateCoverLetter: "/api/halal-file/cover/update",
    updateCoverOption: "/api/halal-file/cover/option",
    previewCoverLetter: (id: number) => `/api/halal-file/cover/preview/${id}`,
    getRawMaterialList: (id: number) => `/api/halal-file/${id}/raw-material`,
    getRawMaterialCertList: (id: number) => `/api/halal-file/${id}/raw-material-cert`,
    checkRawMaterial: (id: number) => `/api/halal-file/${id}/raw-material/check`,
    generate: (id: number) => `/api/halal-file/final-file/${id}`,
    finalize: (id: number) => `/api/halal-file/finalize-file/${id}`,
    download: (id: number) => `/api/halal-file/final-file/${id}/download`,
    getcode: "/api/halal-file/access/code",
    access: "/api/halal-file/access",
    addComment: "/api/halal-file/access/add-comment",
    updateComment: "/api/halal-file/access/update-comment",
    deleteComment: "/api/halal-file/access/delete-comment",
    searchHalalFileActionLog: "/api/halal-file/search/action-log"
  },
  siteLocation: {
    search: "/v2/api/site-location/search",
    searchAll: "/v2/api/site-location/search-all",
    create: "/v2/api/site-location/create",
    update: "/v2/api/site-location/update",
    delete: "/v2/api/site-location/delete"
  },
  oeeDevice: {
    saveLayout: "/v2/api/oee-device/save-layout",
    getLayout: (organizationId: number, siteId?: number | null, siteLocationId?: number | null) => {
      let url = `/v2/api/oee-device/get-layout?organizationId=${organizationId}`;
      if (siteId) url += `&siteId=${siteId}`;
      if (siteLocationId) url += `&siteLocationId=${siteLocationId}`;
      return url;
    }
  },
  systemConfig: {
    deleteOrganizationPackageAssignMenu: (organizationPackageId: number, menuId: number) =>
      `/v2/api/organization-package/delete-menu-by-package/${organizationPackageId}/${menuId}`,
    getOrganizationPackageAssignMenu: (organizationPackageId: number) =>
      `/v2/api/organization-package/menu-list-by-package-id/${organizationPackageId}`,
    updateOrganizationPackageAssignMenu: "/v2/api/organization-package/update-menu-list-by-package",
    getAllOrganizationPackages: "/v2/api/organization-package/all",
    searchOrganizationPackage: "/v2/api/organization-package/search",
    deleteOrganizationPackage: "/v2/api/organization-package/delete",
    createOrganizationPackage: "/v2/api/organization-package/create",
    updateOrganizationPackage: "/v2/api/organization-package/update",
    getOrganizationPackageById: (id: number) => `/v2/api/organization-package/${id}`,
    industryCertificationName: {
      search: "/v2/api/industry/certification-name/super-admin/search",
      getById: (id: number) => `/v2/api/industry/certification-name/super-admin/${id}`,
      create: "/v2/api/industry/certification-name/super-admin/create",
      update: "/v2/api/industry/certification-name/super-admin/update",
      delete: "/v2/api/industry/certification-name/super-admin/delete"
    }
  },
  fleet: {
    devices: "/api/fleet/devices",
    devicesSummary: "/api/fleet/devices/summary",
    devicesRegister: "/api/fleet/devices/register",
    devicesUpdate: (hw: string) => `/api/fleet/devices/${encodeURIComponent(hw)}`,
    devicesDelete: (hw: string) => `/api/fleet/devices/${encodeURIComponent(hw)}`,
    devicesSeed: "/api/fleet/devices/seed",
    status: "/api/fleet/fleet/status",
    historyMeta: "/api/fleet/history/meta",
    historyRange: "/api/fleet/history/range",
    historyAggregated: "/api/fleet/history/aggregated",
    deviceSettings: "/api/fleet/device_settings",
    deviceSettingsSave: "/api/fleet/device_settings/save",
    alarmLogRecent: "/api/fleet/alarm_log/recent",
    alarmLogByDate: "/api/fleet/alarm_log/by_date",
    alarmLogTest: (hardwareId: string) => `/api/fleet/alarm_log/test/${hardwareId}`,
    sensorReadings: "/api/fleet/alarm_log/sensor_readings",
    tripsList: "/api/fleet/trips/list",
    tripById: (id: number) => `/api/fleet/trips/${id}`,
    tripsOpen: "/api/fleet/trips/open",
    tripsClose: (id: number) => `/api/fleet/trips/${id}/close`,
    tripsSave: "/api/fleet/trips/save",
    locations: "/api/fleet/locations",
    locationsSave: "/api/fleet/locations/save",
    locationsDelete: (id: number) => `/api/fleet/locations/${id}`,
    navMenus: "/api/fleet/nav/menus",
    batteryForecast: "/api/fleet/realtime/battery-forecast",
    breachSummary: "/api/fleet/alarm_log/breach-summary"
  }
};

export default CONST;
