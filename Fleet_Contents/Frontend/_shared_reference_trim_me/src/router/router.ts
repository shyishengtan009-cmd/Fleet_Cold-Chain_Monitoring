// import { directRouteExecRole } from "@/utils/TemperatureHumidity/directRouteExecRole";
import { createRouter, createWebHistory, RouteLocationNormalized } from "vue-router";
import { useCurrentUserStore } from "../store/currentUser";
import { usePermissionStore } from "../store/permissions";
import "./router.d"; // import ambient module
// import api from "../helpers/api";

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: "/:pathMatch(.*)*",
      redirect: "/login",
      meta: {
        requireLogin: false,
        requiredPermissions: ["Dashboard.View"]
      }
    },
    // {
    //   path: "/login",
    //   component: () => import("../pages/LoginMain.vue"),
    //   meta: {
    //     requireLogin: false,
    //     requiredPermissions: []
    //   }
    // },
    {
      path: "/login",
      component: () => import("../pages/LoginMainV3.vue"),
      meta: {
        requireLogin: false,
        requiredPermissions: []
      }
    },
    {
      path: "/delete",
      component: () => import("../pages/DeleteAccount/DeleteAccountPage.vue"),
      meta: {
        requireLogin: false,
        requiredPermissions: []
      }
    },
    {
      path: "/block-chain/:id",
      // component: () => import("../pages/BlockChain/BlockChainMain.vue"),
      component: () => import("../pages/BlockChain/BlockChainMain.vue"),
      meta: {
        requireLogin: false,
        requiredPermissions: []
      }
    },
    // {
    //   path: "/forget-password",
    //   component: () => import("../pages/ForgetPassword.vue"),
    //   meta: {
    //     requireLogin: false,
    //     requiredPermissions: []
    //   }
    // },
    // {
    //   path: "/forget-password-v2",
    //   component: () => import("../pages/ForgetPasswordV2.vue"),
    //   meta: {
    //     requireLogin: false,
    //     requiredPermissions: []
    //   }
    // },
    {
      path: "/forget-password-v3",
      component: () => import("../pages/ForgetPasswordV3.vue"),
      meta: {
        requireLogin: false,
        requiredPermissions: []
      }
    },
    // {
    //   path: "/reset-password",
    //   component: () => import("../pages/ResetPassword.vue"),
    //   meta: {
    //     requireLogin: false,
    //     requiredPermissions: []
    //   }
    // },
    {
      path: "/reset-password",
      component: () => import("../pages/ResetPasswordV2.vue"),
      meta: {
        requireLogin: false,
        requiredPermissions: []
      }
    },
    {
      path: "/validate-halal-file",
      component: () => import("../pages/Application/HalalFileAccess.vue"),
      meta: {
        requireLogin: false,
        requiredPermissions: []
      }
    },
    {
      path: "/dashboard",
      component: () => import("../pages/Dashbroad/DashboardMainV2.vue"),
      meta: {
        requireLogin: true,
        requiredPermissions: ["Dashboard.View"]
      }
    },
    {
      path: "/document",
      meta: {
        requireLogin: true,
        requiredPermissions: []
      },
      children: [
        {
          path: "compliance",
          component: () => import("../pages/DocumentCompliance.vue"),
          meta: {
            menuName: "Compliance Document",
            requireLogin: true,
            requiredPermissions: ["DocumentCompliance.View"]
          }
        },
        {
          path: "compliance/:id",
          component: () => import("../pages/DocumentComplianceDetail.vue"),
          meta: {
            menuName: "Compliance Document",
            requireLogin: true,
            requiredPermissions: ["DocumentCompliance.View"]
          }
        },
        {
          path: "compliance/new",
          component: () => import("../pages/DocumentComplianceNew.vue"),
          meta: {
            menuName: "Compliance Document",
            requireLogin: true,
            requiredPermissions: ["DocumentCompliance.Create"]
          }
        },
        {
          path: "monitoring",
          component: () => import("../pages/DocumentMonitoring.vue"),
          meta: {
            menuName: "Monitoring Log & Report",
            requireLogin: true,
            requiredPermissions: ["DocumentMonitoring.View"]
          }
        },
        {
          path: "monitoring/:id",
          component: () => import("../pages/DocumentMonitoringDetail.vue"),
          meta: {
            menuName: "Monitoring Log & Report",
            requireLogin: true,
            requiredPermissions: ["DocumentMonitoring.View"]
          }
        },
        {
          path: "monitoring/new",
          component: () => import("../pages/DocumentMonitoringNew.vue"),
          meta: {
            menuName: "Monitoring Log & Report",
            requireLogin: true,
            requiredPermissions: ["DocumentMonitoring.Create"]
          }
        },
        {
          path: "pest-control",
          children: [
            {
              path: "",
              component: () =>
                import("../pages/PestControl/PestControlSchedule/PestControlScheduleList.vue"),
              meta: {
                menuName: "Pest Control Schedule"
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/PestControl/PestControlSchedule/CreatePestControlSchedule.vue"),
              meta: {
                menuName: "Pest Control Schedule"
              }
            },
            {
              path: ":id",
              component: () =>
                import("../pages/PestControl/PestControlSchedule/PestControlScheduleDetail.vue"),
              meta: {
                menuName: "Pest Control Schedule"
              }
            }
          ]
        },
        {
          path: "pest-control-inspection",
          children: [
            {
              path: "",
              component: () =>
                import("../pages/PestControl/PestControlInspection/PestControlInspectionList.vue"),
              meta: {
                menuName: "Pest Control Inspection"
              }
            },
            {
              path: "new",
              component: () =>
                import(
                  "../pages/PestControl/PestControlInspection/CreatePestControlInspection.vue"
                ),
              meta: {
                menuName: "Pest Control Inspection"
              }
            },
            {
              path: ":id",
              component: () =>
                import(
                  "../pages/PestControl/PestControlInspection/PestControlInspectionDetail.vue"
                ),
              meta: {
                menuName: "Pest Control Inspection"
              }
            }
          ]
        }
      ]
    },
    {
      path: "/application",
      meta: {
        requireLogin: true,
        requiredPermissions: ["Application.View"]
      },
      children: [
        {
          path: "",
          component: () => import("@/pages/Application/ApplicationList.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["Application.View"]
          }
        },
        {
          path: "new",
          component: () => import("@/pages/Application/ApplicationNew.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["Application.Create"]
          }
        },
        {
          path: "detail/:id",
          component: () => import("@/pages/Application/ApplicationDetail.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["Application.View"]
          }
        },
        {
          path: "halalfile/:id",
          component: () => import("@/pages/Application/HalalFile.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["Application.View"]
          }
        },
        {
          path: "halalfileview",
          component: () => import("@/pages/Application/HalalFileAccess.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["Application.View"]
          }
        }
      ]
    },
    {
      path: "/certification",
      meta: {
        requireLogin: true,
        requiredPermissions: []
      },
      children: [
        {
          path: "industry-certification",
          meta: { menuName: "Industry Certification" },
          children: [
            {
              path: "",
              component: () =>
                import(
                  "../pages/Certification/IndustryCertification/IndustryCertificationManagement.vue"
                ),
              meta: {
                // requiredPermissions: ["IndustryCertification.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import(
                  "../pages/Certification/IndustryCertification/CreateIndustryCertification.vue"
                ),
              meta: {
                menuName: "Industry Certification"
                // requiredPermissions: ["IndustryCertification.Create"]
              }
            },
            {
              path: ":id",
              component: () =>
                import(
                  "../pages/Certification/IndustryCertification/IndustryCertificationDetail.vue"
                ),
              meta: {
                menuName: "Industry Certification"
                // requiredPermissions: ["IndustryCertification.View"]
              }
            }
          ]
        }
      ]
    },
    {
      path: "/audit",
      meta: {
        requireLogin: true,
        requiredPermissions: []
      },
      children: [
        {
          path: "schedule",
          meta: {
            menuName: "Audit Schedule",
            requireLogin: true,
            requiredPermissions: ["AuditSchedule.View"]
          },
          children: [
            {
              path: "internal",
              component: () => import("../pages/Audit/Schedule/ScheduleListV2.vue"),
              meta: {
                menuName: "Audit Schedule",
                requireLogin: true,
                requiredPermissions: ["AuditSchedule.View"]
              }
            },
            {
              path: "supplier",
              component: () => import("../pages/Audit/Schedule/ScheduleListV2.vue"),
              meta: {
                menuName: "Audit Schedule",
                requireLogin: true,
                requiredPermissions: ["AuditSchedule.View"]
              }
            },
            {
              path: ":type/new",
              component: () => import("../pages/Audit/Schedule/CreateScheduleV2.vue"),
              meta: {
                menuName: "Audit Schedule",
                requireLogin: true,
                requiredPermissions: ["AuditSchedule.Create"]
              }
            },
            {
              path: ":type/detail/:id",
              component: () => import("../pages/Audit/Schedule/EditScheduleV2.vue"),
              meta: {
                menuName: "Audit Schedule",
                requireLogin: true,
                requiredPermissions: ["AuditSchedule.View"]
              }
            },
            {
              path: "",
              component: () => import("../pages/Audit/Schedule/ScheduleList.vue"),
              meta: {
                menuName: "Audit Schedule",
                requireLogin: true,
                requiredPermissions: ["AuditSchedule.View"]
              }
            }
          ]
        },
        {
          path: "report",
          meta: {
            menuName: "Audit Report",
            requireLogin: true,
            requiredPermissions: ["AuditReport.View"]
          },
          children: [
            {
              path: "internal",
              component: () => import("../pages/Audit/Reports/ReportListV2.vue"),
              meta: {
                menuName: "Audit Report",
                requireLogin: true,
                requiredPermissions: ["AuditReport.View"]
              }
            },
            {
              path: "supplier",
              component: () => import("../pages/Audit/Reports/ReportListV2.vue"),
              meta: {
                menuName: "Audit Report",
                requireLogin: true,
                requiredPermissions: ["AuditReport.View"]
              }
            },
            {
              path: "internal/detail/:id",
              component: () => import("../pages/Audit/ReportV2/AuditReportInfomationV2.vue"),
              meta: {
                menuName: "Audit Report",
                requireLogin: true,
                requiredPermissions: ["AuditReport.View"]
              }
            },
            {
              path: "supplier/detail/:id",
              component: () => import("../pages/Audit/ReportV2/AuditReportInfomationV2.vue"),
              meta: {
                menuName: "Audit Report",
                requireLogin: true,
                requiredPermissions: ["AuditReport.View"]
              }
            },
            {
              path: "certification-body/detail/:id",
              component: () => import("../pages/Audit/ReportV2/AuditReportInfomationV3.vue"),
              meta: {
                menuName: "Audit Report",
                requireLogin: true,
                requiredPermissions: ["AuditReport.View"]
              }
            },
            {
              path: "buyer-audit/detail/:id",
              component: () =>
                import("../pages/Audit/ReportV2/AuditReportInfomationBuyerAudit.vue"),
              meta: {
                menuName: "Audit Report",
                requireLogin: true,
                requiredPermissions: ["AuditReport.View"]
              }
            },
            {
              path: "third-party/detail/:id",
              component: () =>
                import("../pages/Audit/ReportV2/AuditReportInformationThirdParty.vue"),
              meta: {
                menuName: "Audit Report",
                requireLogin: true,
                requiredPermissions: ["AuditReport.View"]
              }
            },
            {
              path: "",
              component: () => import("../pages/Audit/Reports/ReportList.vue"),
              meta: {
                menuName: "Audit Report",
                requireLogin: true,
                requiredPermissions: ["AuditReport.View"]
              }
            }
          ]
        },
        {
          path: "checklist/template",
          meta: {
            menuName: "Audit Checklist Template",
            requireLogin: true,
            requiredPermissions: ["ChecklistTemplate.View"]
          },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/Audit/ChecklistTemplate/AuditChecklistTemplate.vue"),
              meta: {
                menuName: "Audit Checklist Template",
                requireLogin: true,
                requiredPermissions: ["ChecklistTemplate.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/Audit/ChecklistTemplate/CreateChecklistTemplate.vue"),
              meta: {
                menuName: "Audit Checklist Template",
                requireLogin: true,
                requiredPermissions: ["ChecklistTemplate.View"]
              }
            },
            {
              path: "upload",
              component: () =>
                import("../pages/Audit/ChecklistTemplate/UploadChecklistTemplate.vue"),
              meta: {
                menuName: "Audit Checklist Template",
                requireLogin: true,
                requiredPermissions: ["ChecklistTemplate.View"]
              }
            },
            {
              path: "detail/:id",
              component: () => import("../pages/Audit/ChecklistTemplate/EditChecklistTemplate.vue"),
              meta: {
                menuName: "Audit Checklist Template",
                requireLogin: true,
                requiredPermissions: ["ChecklistTemplate.View"]
              }
            }
          ]
        }
      ]
    },
    {
      path: "/certification-body-audit",
      meta: {
        requireLogin: true,
        requiredPermissions: []
      },
      children: [
        {
          path: "schedule",
          meta: {
            menuName: "Audit Schedule",
            requireLogin: true,
            requiredPermissions: ["AuditSchedule.View"]
          },
          children: [
            {
              path: "",
              component: () => import("../pages/CertificationBodyAudit/Schedule/ScheduleList.vue"),
              meta: {
                menuName: "Audit Schedule",
                requireLogin: true,
                requiredPermissions: ["AuditSchedule.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/CertificationBodyAudit/Schedule/CreateScheduleV2.vue"),
              meta: {
                menuName: "Audit Schedule",
                requireLogin: true,
                requiredPermissions: ["AuditSchedule.Create"]
              }
            },
            {
              path: "detail/:id",
              component: () =>
                import("../pages/CertificationBodyAudit/Schedule/EditScheduleV2.vue"),
              meta: {
                menuName: "Audit Schedule",
                requireLogin: true,
                requiredPermissions: ["AuditSchedule.View"]
              }
            }
          ]
        },
        {
          path: "report",
          meta: {
            menuName: "Audit Report",
            requireLogin: true,
            requiredPermissions: ["AuditReport.View"]
          },
          children: [
            {
              path: "",
              component: () => import("../pages/CertificationBodyAudit/Reports/ReportList.vue"),
              meta: {
                menuName: "Audit Report",
                requireLogin: true,
                requiredPermissions: ["AuditReport.View"]
              }
            },
            {
              path: "detail/:id",
              component: () =>
                import("../pages/CertificationBodyAudit/ReportV2/AuditReportInfomationV2.vue"),
              meta: {
                menuName: "Audit Report",
                requireLogin: true,
                requiredPermissions: ["AuditReport.View"]
              }
            }
          ]
        }
      ]
    },
    {
      path: "/non-conformance",
      meta: {
        requireLogin: true,
        requiredPermissions: ["NonConformance.View"]
      },
      children: [
        {
          path: "",
          component: () => import("../pages/NonConformance/NonConformance.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["NonConformance.View"]
          }
        },
        {
          path: "new",
          component: () => import("../pages/NonConformance/CreateNonConformance.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["NonConformance.Create"]
          }
        },
        {
          path: "detail/:id",
          component: () => import("../pages/NonConformance/EditNonConformance.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["NonConformance.View"]
          }
        }
      ]
    },
    {
      path: "/operation",
      meta: { requireLogin: true },
      children: [
        {
          path: "raw-material",
          meta: { menuName: "Raw Material Master List" },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/Operation/MaterialMasterList/RawMaterialManagement.vue"),
              meta: {
                requiredPermissions: ["RawMaterial.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/Operation/MaterialMasterList/CreateRawMaterial.vue"),
              meta: {
                requiredPermissions: ["RawMaterial.Create"]
              }
            },
            {
              path: ":id",
              component: () => import("../pages/Operation/MaterialMasterList/EditRawMaterial.vue"),
              meta: {
                requiredPermissions: ["RawMaterial.View"]
              }
            }
          ]
        },
        {
          path: "raw-material-match",
          meta: { menuName: "Raw Material Matching" },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/Operation/RawMaterialMatch/RawMaterialManagement.vue"),
              meta: {
                requiredPermissions: ["RawMaterialMatching.View"]
              }
            }
          ]
        },
        // {
        //   path: "raw-material-matching-upload-match",
        //   meta: { menuName: "Raw Material Upload Master List" },
        //   children: [
        //     {
        //       path: "",
        //       component: () =>
        //         import(
        //           "../pages/Operation/RawMaterialMatchingUploadMatch/RawMaterialManagement.vue"
        //         ),
        //       meta: {
        //         requiredPermissions: ["RawMaterialMatchingUpload.View"]
        //       }
        //     }
        //   ]
        // },
        {
          path: "raw-material-certificate",
          meta: { menuName: "Raw Material Halal Certificate" },
          children: [
            {
              path: "",
              component: () => import("../pages/RawMaterialCertificateManagement.vue"),
              meta: {
                requiredPermissions: ["RawMaterialCertificate.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/Operation/RawMaterialCertificate/RawMaterialCertificateNew.vue"),
              meta: {
                menuName: "Raw Material Halal Certificate",
                requiredPermissions: ["RawMaterialCertificate.Create"]
              }
            },
            {
              path: ":id",
              component: () =>
                import(
                  "../pages/Operation/RawMaterialCertificate/RawMaterialCertificateDetail.vue"
                ),
              meta: {
                menuName: "Raw Material Halal Certificate",
                requiredPermissions: ["RawMaterialCertificate.View"]
              }
            }
          ]
        },
        {
          path: "product-certificate",
          meta: { menuName: "Product Halal Certificate" },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/Operation/ProductCertificate/ProductCertificateManagement.vue"),
              meta: {
                requiredPermissions: ["ProductCertificate.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/Operation/ProductCertificate/ProductCertificateNew.vue"),
              meta: {
                requiredPermissions: ["ProductCertificate.Create"]
              }
            },
            {
              path: ":id",
              component: () =>
                import("../pages/Operation/ProductCertificate/ProductCertificateDetail.vue"),
              meta: {
                requiredPermissions: ["ProductCertificate.View"]
              }
            }
          ]
        },
        {
          path: "product",
          meta: { menuName: "Product Master List" },
          children: [
            {
              path: "",
              component: () => import("../pages/Operation/ProductMasterList/ProductManagement.vue"),
              meta: {
                requiredPermissions: ["Product.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/Operation/ProductMasterList/ProductNew.vue"),
              meta: {
                requiredPermissions: ["Product.Create"]
              }
            },
            {
              path: ":id",
              component: () => import("../pages/Operation/ProductMasterList/ProductDetail.vue"),
              meta: {
                requiredPermissions: ["Product.View"]
              }
            }
          ]
        },
        {
          path: "product-menu",
          meta: { menuName: "Menu Master List" },
          children: [
            {
              path: "",
              component: () => import("../pages/Operation/MenuMasterList/MenuManagement.vue"),
              meta: {
                requiredPermissions: ["MenuMaster.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/Operation/MenuMasterList/MenuNew.vue"),
              meta: {
                requiredPermissions: ["MenuMaster.Create"]
              }
            },
            {
              path: ":id",
              component: () => import("../pages/Operation/MenuMasterList/MenuDetail.vue"),
              meta: {
                requiredPermissions: ["MenuMaster.View"]
              }
            }
          ]
        },
        {
          path: "product-trading",
          meta: { menuName: "Trading Product Master List" },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/Operation/ProductTradingMasterList/ProductTradingManagement.vue"),
              meta: {
                requiredPermissions: ["TradingProduct.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/Operation/ProductTradingMasterList/ProductNew.vue"),
              meta: {
                requiredPermissions: ["TradingProduct.Create"]
              }
            },
            {
              path: ":id",
              component: () =>
                import("../pages/Operation/ProductTradingMasterList/ProductDetail.vue"),
              meta: {
                requiredPermissions: ["TradingProduct.View"]
              }
            }
          ]
        },
        {
          path: "certificate-upload",
          meta: { menuName: "Certificate Upload" },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/Operation/CertificateUpload/CertificateUploadManagement.vue"),
              meta: {
                requiredPermissions: ["CertificateUpload.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/Operation/CertificateUpload/CertificateUploadNew.vue"),
              meta: {
                requiredPermissions: ["CertificateUpload.Create"]
              }
            },
            {
              path: ":id",
              component: () =>
                import("../pages/Operation/CertificateUpload/CertificateUploadDetail.vue"),
              meta: {
                requiredPermissions: ["CertificateUpload.View"]
              }
            }
          ]
        },
        // {
        //   path: "certificate-matching-upload",
        //   meta: { menuName: "Certificate Matching Upload" },
        //   children: [
        //     {
        //       path: "",
        //       component: () =>
        //         import(
        //           "../pages/Operation/CertificateMatchingUpload/CertificateMatchingUploadManagement.vue"
        //         ),
        //       meta: {
        //         requiredPermissions: ["AICertificateMatchingUpload.View"]
        //       }
        //     },
        //     {
        //       path: "new",
        //       component: () =>
        //         import(
        //           "../pages/Operation/CertificateMatchingUpload/CertificateMatchingUploadNew.vue"
        //         ),
        //       meta: {
        //         requiredPermissions: ["AICertificateMatchingUpload.Create"]
        //       }
        //     },
        //     {
        //       path: ":id",
        //       component: () =>
        //         import(
        //           "../pages/Operation/CertificateMatchingUpload/CertificateMatchingUploadDetail.vue"
        //         ),
        //       meta: {
        //         requiredPermissions: ["AICertificateMatchingUpload.View"]
        //       }
        //     }
        //   ]
        // },
        // {
        //   path: "raw-material-matching-upload",
        //   meta: { menuName: "Raw Material Matching Upload" },
        //   children: [
        //     {
        //       path: "",
        //       component: () =>
        //         import(
        //           "../pages/Operation/RawMaterialMatchingUpload/RawMaterialMatchingUploadManagement.vue"
        //         ),
        //       meta: {
        //         requiredPermissions: ["Product.View"]
        //       }
        //     },
        //     {
        //       path: "new",
        //       component: () =>
        //         import(
        //           "../pages/Operation/RawMaterialMatchingUpload/RawMaterialMatchingUploadNew.vue"
        //         ),
        //       meta: {
        //         requiredPermissions: ["Product.Create"]
        //       }
        //     },
        //     {
        //       path: ":id",
        //       component: () =>
        //         import(
        //           "../pages/Operation/RawMaterialMatchingUpload/RawMaterialMatchingUploadDetail.vue"
        //         ),
        //       meta: {
        //         requiredPermissions: ["Product.View"]
        //       }
        //     }
        //   ]
        // },
        {
          path: "certificate-request",
          meta: { requireLogin: true },
          children: [
            {
              path: "verification",
              meta: { menuName: "Certificate Request Veritification" },
              children: [
                {
                  path: "",
                  component: () =>
                    import(
                      "../pages/Operation/CertificateRequestVerification/CertificateRequestVerificationList.vue"
                    ),
                  meta: {
                    requiredPermissions: ["CertificateRequest.Verify"]
                  }
                },
                {
                  path: "view/:id",
                  component: () =>
                    import(
                      "../pages/Operation/CertificateRequestVerification/ViewCertificateRequest.vue"
                    ),
                  meta: {
                    requiredPermissions: ["CertificateRequest.Verify"]
                  }
                },
                {
                  path: "verify/:id",
                  component: () =>
                    import(
                      "../pages/Operation/CertificateRequestVerification/VerifyCertificateRequest.vue"
                    ),
                  meta: {
                    requiredPermissions: ["CertificateRequest.Verify"]
                  }
                }
              ]
            },
            {
              path: "supplier",
              meta: { menuName: "Certificate Request" },
              children: [
                {
                  path: "",
                  component: () =>
                    import("../pages/Operation/CertificateRequestHalal/CertificateRequestList.vue"),
                  meta: {
                    requiredPermissions: ["CertificateRequest.View"]
                  }
                },
                {
                  path: ":id",
                  component: () =>
                    import("../pages/Operation/CertificateRequestHalal/EditCertificateRequest.vue"),
                  meta: {
                    requiredPermissions: ["CertificateRequest.View"]
                  }
                },
                {
                  path: "view/:id",
                  component: () =>
                    import("../pages/Operation/CertificateRequestHalal/ViewCertificateRequest.vue"),
                  meta: {
                    requiredPermissions: ["CertificateRequest.View"]
                  }
                },
                {
                  path: "approve/:id",
                  component: () =>
                    import(
                      "../pages/Operation/CertificateRequestHalal/ApproveCertificateRequest.vue"
                    ),
                  meta: {
                    requiredPermissions: ["CertificateRequest.View"]
                  }
                },
                {
                  path: "reject/:id",
                  component: () =>
                    import(
                      "../pages/Operation/CertificateRequestHalal/RejectCertificateRequest.vue"
                    ),
                  meta: {
                    requiredPermissions: ["CertificateRequest.View"]
                  }
                }
              ]
            },
            {
              path: "certificate-request-to-supplier",
              meta: { menuName: "Certificate Request to Supplier" },
              children: [
                {
                  path: "",
                  component: () =>
                    import(
                      "../pages/Operation/CertificateRequestToSupplier/CertificateRequestVerificationList.vue"
                    ),
                  meta: {
                    requiredPermissions: ["CertificateRequest.Verify"]
                  }
                },
                {
                  path: "view/:id",
                  component: () =>
                    import(
                      "../pages/Operation/CertificateRequestToSupplier/ViewCertificateRequest.vue"
                    ),
                  meta: {
                    requiredPermissions: ["CertificateRequest.Verify"]
                  }
                },
                {
                  path: "verify/:id",
                  component: () =>
                    import(
                      "../pages/Operation/CertificateRequestToSupplier/VerifyCertificateRequest.vue"
                    ),
                  meta: {
                    requiredPermissions: ["CertificateRequest.Verify"]
                  }
                }
              ]
            },
            {
              path: "certificate-request-from-customer",
              meta: { menuName: "Certificate Request" },
              children: [
                {
                  path: "",
                  component: () =>
                    import(
                      "../pages/Operation/CertificateRequestFromCustomer/CertificateRequestList.vue"
                    ),
                  meta: {
                    requiredPermissions: ["CertificateRequest.View"]
                  }
                },
                {
                  path: ":id",
                  component: () =>
                    import(
                      "../pages/Operation/CertificateRequestFromCustomer/EditCertificateRequest.vue"
                    ),
                  meta: {
                    requiredPermissions: ["CertificateRequest.View"]
                  }
                },
                {
                  path: "view/:id",
                  component: () =>
                    import(
                      "../pages/Operation/CertificateRequestFromCustomer/ViewCertificateRequest.vue"
                    ),
                  meta: {
                    requiredPermissions: ["CertificateRequest.View"]
                  }
                },
                {
                  path: "approve/:id",
                  component: () =>
                    import(
                      "../pages/Operation/CertificateRequestFromCustomer/ApproveCertificateRequest.vue"
                    ),
                  meta: {
                    requiredPermissions: ["CertificateRequest.View"]
                  }
                },
                {
                  path: "reject/:id",
                  component: () =>
                    import(
                      "../pages/Operation/CertificateRequestFromCustomer/RejectCertificateRequest.vue"
                    ),
                  meta: {
                    requiredPermissions: ["CertificateRequest.View"]
                  }
                }
              ]
            }
          ]
        },
        {
          path: "trading-product-certificate",
          meta: { menuName: "Trading Product Halal Certificate" },
          children: [
            {
              path: "",
              component: () =>
                import(
                  "../pages/Operation/ProductTradingCertificate/ProductTradingCertificateManagement.vue"
                ),
              meta: {
                requiredPermissions: ["TradingProductCertificate.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import(
                  "../pages/Operation/ProductTradingCertificate/ProductTradingCertificateNew.vue"
                ),
              meta: {
                requiredPermissions: ["TradingProductCertificate.Create"]
              }
            },
            {
              path: ":id",
              component: () =>
                import(
                  "../pages/Operation/ProductTradingCertificate/ProductTradingCertificateDetail.vue"
                ),
              meta: {
                requiredPermissions: ["TradingProductCertificate.View"]
              }
            }
          ]
        },
        {
          path: "food-premise-certificate",
          meta: { menuName: "Food Premise Halal Certificate" },
          children: [
            {
              path: "",
              component: () =>
                import(
                  "../pages/Operation/FoodPremiseCertificate/FoodPremiseCertificateManagement.vue"
                ),
              meta: {
                requiredPermissions: ["FoodPremiseCertificate.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/Operation/FoodPremiseCertificate/FoodPremiseCertificateNew.vue"),
              meta: {
                requiredPermissions: ["FoodPremiseCertificate.Create"]
              }
            },
            {
              path: ":id",
              component: () =>
                import(
                  "../pages/Operation/FoodPremiseCertificate/FoodPremiseCertificateDetail.vue"
                ),
              meta: {
                requiredPermissions: ["FoodPremiseCertificate.View"]
              }
            }
          ]
        }
      ]
    },
    {
      path: "/ai-maching",
      meta: { requireLogin: true },
      children: [
        {
          path: "raw-material-matching-upload-match",
          meta: { menuName: "Raw Material Upload Master List" },
          children: [
            {
              path: "",
              component: () =>
                import(
                  "../pages/Operation/RawMaterialMatchingUploadMatch/RawMaterialManagement.vue"
                ),
              meta: {
                requiredPermissions: ["AIRawMaterial.View"]
              }
            }
          ]
        },
        // {
        //   path: "raw-material-matching-upload",
        //   meta: { menuName: "Raw Material Matching Upload" },
        //   children: [
        //     {
        //       path: "",
        //       component: () =>
        //         import(
        //           "../pages/Operation/RawMaterialMatchingUpload/RawMaterialMatchingUploadManagement.vue"
        //         ),
        //       meta: {
        //         requiredPermissions: ["AICertificateMatchingUpload.View"]
        //       }
        //     },
        //     {
        //       path: "new",
        //       component: () =>
        //         import(
        //           "../pages/Operation/RawMaterialMatchingUpload/RawMaterialMatchingUploadNew.vue"
        //         ),
        //       meta: {
        //         requiredPermissions: ["RawMaterialMatchingUpload.Create"]
        //       }
        //     },
        //     {
        //       path: ":id",
        //       component: () =>
        //         import(
        //           "../pages/Operation/RawMaterialMatchingUpload/RawMaterialMatchingUploadDetail.vue"
        //         ),
        //       meta: {
        //         requiredPermissions: ["RawMaterialMatchingUpload.View"]
        //       }
        //     }
        //   ]
        // },
        {
          path: "certificate-matching-upload",
          meta: { menuName: "Certificate Matching Upload" },
          children: [
            {
              path: "",
              component: () =>
                import(
                  "../pages/Operation/CertificateMatchingUpload/CertificateMatchingUploadManagement.vue"
                ),
              meta: {
                requiredPermissions: ["AICertificateMatchingUpload.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import(
                  "../pages/Operation/CertificateMatchingUpload/CertificateMatchingUploadNew.vue"
                ),
              meta: {
                requiredPermissions: ["AICertificateMatchingUpload.Create"]
              }
            },
            {
              path: ":id",
              component: () =>
                import(
                  "../pages/Operation/CertificateMatchingUpload/CertificateMatchingUploadDetail.vue"
                ),
              meta: {
                requiredPermissions: ["AICertificateMatchingUpload.View"]
              }
            }
          ]
        }
      ]
    },
    {
      path: "/report",
      meta: { requireLogin: true },
      children: [
        {
          path: "raw-material",
          meta: { menuName: "Raw Material Master List" },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/Report/MaterialMasterList/RawMaterialManagement.vue"),
              meta: {
                requiredPermissions: ["ReportRawMaterial.View"]
              }
            }
          ]
        },
        // {
        //   path: "product",
        //   meta: { menuName: "Product Master List" },
        //   children: [
        //     {
        //       path: "",
        //       component: () => import("../pages/Report/ProductMasterList/ProductManagement.vue"),
        //       meta: {
        //         requiredPermissions: ["Product.View"]
        //       }
        //     }
        //   ]
        // },
        {
          path: "product-trading",
          meta: { menuName: "Trading Product Master List" },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/Report/ProductTradingMasterList/ProductTradingManagement.vue"),
              meta: {
                requiredPermissions: ["ReportTradingProduct.View"]
              }
            }
          ]
        },
        {
          path: "trading-product-certificate",
          meta: { menuName: "Trading Product Halal Certificate" },
          children: [
            {
              path: "",
              component: () =>
                import(
                  "../pages/Report/ProductTradingCertificate/ProductTradingCertificateManagement.vue"
                ),
              meta: {
                requiredPermissions: ["ReportTradingProduct.View"]
              }
            }
          ]
        },
        {
          path: "supplier-risk-score-report",
          children: [
            {
              path: "",
              component: () => import("../pages/Report/Supplier/SupplierManagement.vue"),
              meta: {
                requiredPermissions: ["ReportSupplierRiskAssessmentScore.View"]
              }
            }
          ]
        },
        {
          path: "expenses-report",
          meta: { menuName: "Expenses Report" },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/Purchasing/ExpensesReport/ExpensesReportManagement.vue"),
              meta: {
                requiredPermissions: ["ExpensesReport.View"]
              }
            }
          ]
        },
        {
          path: "yearly-non-conformance-trend",
          meta: {
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () =>
                import(
                  "../pages/Report/YearlyNonConformanceTrend/YearlyNonComformanceTrendList.vue"
                ),
              meta: {
                requiredPermissions: []
              }
            }
          ]
        },
        {
          path: "pest-control-yearly-summary",
          component: () =>
            import(
              "../pages/PestControl/PestControlYearlySummary/PestControlYearlySummaryList.vue"
            ),
          meta: {
            menuName: "Yearly Pest Control Inspection Summary by Station"
          }
        }
      ]
    },
    {
      path: "/purchasing",
      meta: { requireLogin: true },
      children: [
        {
          path: "supplier",
          children: [
            {
              path: "",
              component: () => import("../pages/Purchasing/Supplier/SupplierManagement.vue"),
              meta: {
                menuName: "Supplier",
                requiredPermissions: ["Supplier.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/Purchasing/Supplier/SupplierNew.vue"),
              meta: {
                menuName: "Supplier",
                requiredPermissions: ["Supplier.Create"]
              }
            },
            {
              path: ":id",
              component: () => import("../pages/Purchasing/Supplier/SupplierDetail.vue"),
              meta: {
                menuName: "Supplier",
                requiredPermissions: ["Supplier.View"]
              }
            }
          ]
        },
        {
          path: "raw-material",
          children: [
            {
              path: "",
              component: () => import("../pages/RawMaterialPurchase.vue"),
              meta: {
                menuName: "Raw Material Purchase",
                requiredPermissions: [["RawMaterialPurchase.View"], ["RawMaterial.View"]]
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/Purchasing/RawMaterialPurchase/RawMaterialPurchaseNew.vue"),
              meta: {
                menuName: "Raw Material Purchase",
                requiredPermissions: ["RawMaterialPurchase.Create"]
              }
            },
            {
              path: ":id",
              component: () =>
                import("../pages/Purchasing/RawMaterialPurchase/RawMaterialPurchaseDetail.vue"),
              meta: {
                menuName: "Raw Material Purchase",
                requiredPermissions: ["RawMaterialPurchase.View"]
              }
            }
          ]
        },
        {
          path: "expenses-master-list",
          meta: { menuName: "Expenses Master List" },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/Purchasing/ExpensesMasterList/ExpensesManagement.vue"),
              meta: {
                requiredPermissions: ["Expenses.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/Purchasing/ExpensesMasterList/ExpensesNew.vue"),
              meta: {
                requiredPermissions: ["Expenses.Create"]
              }
            },
            {
              path: ":id",
              component: () => import("../pages/Purchasing/ExpensesMasterList/ExpensesDetail.vue"),
              meta: {
                requiredPermissions: ["Expenses.View"]
              }
            }
          ]
        }
        // {
        //   path: "manufacturer",
        //   component: () => import("../pages/ManufacturerManagement.vue"),
        //   meta: {
        //     requiredPermissions: ["Manufacturer.View"]
        //   }
        // },
        // {
        //   path: "manufacturer/new",
        //   component: () => import("../pages/ManufacturerNew.vue"),
        //   meta: {
        //     requiredPermissions: ["Manufacturer.Create"]
        //   }
        // },
        // {
        //   path: "manufacturer/:id",
        //   component: () => import("../pages/ManufacturerDetail.vue"),
        //   meta: {
        //     requiredPermissions: ["Manufacturer.View"]
        //   }
        // },
        // {
        //   path: "logistic-provider",
        //   component: () => import("../pages/LogisticProviderManagement.vue"),
        //   meta: {
        //     requiredPermissions: ["LogisticProvider.View"]
        //   }
        // },
        // {
        //   path: "logistic-provider/new",
        //   component: () => import("../pages/LogisticProviderNew.vue"),
        //   meta: {
        //     requiredPermissions: ["LogisticProvider.Create"]
        //   }
        // },
        // {
        //   path: "logistic-provider/:id",
        //   component: () => import("../pages/LogisticProviderDetail.vue"),
        //   meta: {
        //     requiredPermissions: ["LogisticProvider.View"]
        //   }
        // }
      ]
    },
    {
      path: "/monitoring",
      meta: {
        requireLogin: true,
        requiredPermissions: []
      },
      children: [
        // Fleet routes
        {
          path: "tt19-fleet/status",
          component: () =>
            import("../components/Monitoring/TemperatureHumidity/Fleet/FleetStatus.vue"),
          meta: {
            menuName: "Fleet Status",
            requireLogin: true,
            requiredPermissions: ["Dashboard.View"]
          }
        },
        {
          path: "tt19-fleet/dashboard",
          component: () =>
            import("../components/Monitoring/TemperatureHumidity/Fleet/FleetDashboard.vue"),
          meta: {
            menuName: "Fleet Dashboard",
            requireLogin: true,
            requiredPermissions: ["Dashboard.View"]
          }
        },
        {
          path: "tt19-fleet/device-settings",
          component: () =>
            import(
              "../components/Monitoring/TemperatureHumidity/FleetDeviceSettings/DeviceSettings.vue"
            ),
          meta: {
            menuName: "Device Settings",
            requireLogin: true,
            requiredPermissions: ["Dashboard.View"]
          }
        },
        {
          path: "tt19-fleet/real-time",
          component: () =>
            import(
              "../components/Monitoring/TemperatureHumidity/FleetRealTimeMonitoring/RtmMain.vue"
            ),
          meta: {
            menuName: "Fleet Real-Time Monitoring",
            requireLogin: true,
            requiredPermissions: ["Dashboard.View"]
          }
        },
        {
          path: "tt19-fleet/alert",
          component: () =>
            import("../components/Monitoring/TemperatureHumidity/FleetAlert/AlertPage.vue"),
          meta: {
            menuName: "Fleet Alert",
            requireLogin: true,
            requiredPermissions: ["Dashboard.View"]
          }
        },
        {
          path: "temperature-humidity",
          children: [
            {
              path: "",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/TemperatureHumidity.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
              // /* eslint-disable-next-line no-unused-vars */
              // beforeEnter: async (to, from, next) => {
              //   await directRouteExecRole(next);
              // }
            },
            {
              path: ":id",
              component: () =>
                import(
                  "../pages/Monitoring/TemperatureHumidity/TemperatureHumidityDeviceDetail.vue"
                ),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":id/sensor-list",
              component: () => import("../pages/Monitoring/TemperatureHumidity/SensorList.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":id/sensor-list/:deviceId",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/SensorListDetail.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":id/sensor-list/:deviceId/notification",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/Notification/NotificationList.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":id/sensor-list/:deviceId/notification/new",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/Notification/NotificationNew.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":id/sensor-list/:deviceId/notification/:notificationId",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/Notification/NotificationEdit.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":id/report-center",
              component: () => import("../pages/Monitoring/TemperatureHumidity/ReportCenter.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":deviceName/:deviceId",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/TemperatureHumidityEdit.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/TemperatureHumidityNew.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            }
          ]
        },
        {
          path: "typhoid-vaccination",
          children: [
            {
              path: "",
              component: () =>
                import("../pages/Monitoring/TyphoidVaccination/TyphoidVaccinationList.vue"),
              meta: {
                menuName: "Typhoid Vaccination"
              }
            }
          ]
        },
        {
          path: "scada",
          component: () => import("@/pages/Monitoring/SCADA/SCADADashboard.vue"),
          meta: {
            menuName: "SCADA Dashboard"
          }
        }
      ]
    },
    {
      path: "/admin/monitoring",
      meta: {
        requireLogin: true,
        requiredPermissions: []
      },
      children: [
        {
          path: "temperature-humidity",
          children: [
            {
              path: "",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/TemperatureHumidity.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":id",
              component: () =>
                import(
                  "../pages/Monitoring/TemperatureHumidity/TemperatureHumidityDeviceDetail.vue"
                ),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":id/sensor-list",
              component: () => import("../pages/Monitoring/TemperatureHumidity/SensorList.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":id/sensor-list/:deviceId",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/SensorListDetail.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":id/sensor-list/:deviceId/notification",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/Notification/NotificationList.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":id/sensor-list/:deviceId/notification/new",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/Notification/NotificationNew.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":id/sensor-list/:deviceId/notification/:notificationId",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/Notification/NotificationEdit.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/TemperatureHumidityNew.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: ":deviceName/:deviceId",
              component: () =>
                import("../pages/Monitoring/TemperatureHumidity/TemperatureHumidityEdit.vue"),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: "new-customer-gateway",
              component: () =>
                import(
                  "../pages/Monitoring/TemperatureHumidity/TemperatureHumidityCustomerNew.vue"
                ),
              meta: {
                menuName: "Temperature & Humidity"
              }
            },
            {
              path: "edit-customer-gateway/:organizationId/:gatewayName/:gatewayChannelCode",
              component: () =>
                import(
                  "../pages/Monitoring/TemperatureHumidity/TemperatureHumidityCustomerEdit.vue"
                ),
              meta: {
                menuName: "Temperature & Humidity"
              }
            }
          ]
        }
      ]
    },
    {
      path: "/human-resource",
      meta: {
        requireLogin: true,
        requiredPermissions: []
      },
      children: [
        {
          path: "department",
          meta: {
            menuName: "Department",
            requireLogin: true,
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () => import("../pages/HumanResource/Department/DepartmentList.vue"),
              meta: {
                requiredPermissions: ["Department.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/HumanResource/Department/DepartmentNew.vue"),
              meta: {
                requiredPermissions: ["Department.Create"]
              }
            },
            {
              path: "detail/:id",
              component: () => import("../pages/HumanResource/Department/DepartmentDetail.vue"),
              meta: {
                requiredPermissions: ["Department.View"]
              }
            }
          ]
        },
        {
          path: "designation",
          meta: {
            menuName: "Designation",
            requireLogin: true,
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () => import("../pages/HumanResource/Designtion/DesignationList.vue"),
              meta: {
                requiredPermissions: ["Designation.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/HumanResource/Designtion/DesignationNew.vue"),
              meta: {
                requiredPermissions: ["Designation.Create"]
              }
            },
            {
              path: "detail/:id",
              component: () => import("../pages/HumanResource/Designtion/DesignationDetail.vue"),
              meta: {
                requiredPermissions: ["Designation.View"]
              }
            }
          ]
        },
        {
          path: "employee",
          meta: {
            menuName: "Employee",
            requireLogin: true,
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () => import("../pages/HumanResource/Employee/EmployeeList.vue"),
              meta: {
                requiredPermissions: ["Employee.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/HumanResource/Employee/EmployeeNew.vue"),
              meta: {
                requiredPermissions: ["Employee.Create"]
              }
            },
            {
              path: "detail/:id",
              component: () => import("../pages/HumanResource/Employee/EmployeeDetail.vue"),
              meta: {
                requiredPermissions: ["Employee.View"]
              }
            }
          ]
        },
        // employee V2 starts here
        {
          path: "employee-v2",
          meta: {
            menuName: "Employee V2",
            requireLogin: true,
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () => import("../pages/HumanResource/Employee/EmployeeListV2.vue"),
              meta: {
                requiredPermissions: ["Employee.View"]
              }
            },
            {
              path: "detail/:id",
              component: () => import("../pages/HumanResource/Employee/EmployeeDetailV2.vue"),
              meta: {
                requiredPermissions: ["Employee.View"]
              }
            }
          ]
        },
        // employee V2 ends here
        {
          path: "employee-group",
          meta: {
            menuName: "Employee Group",
            requireLogin: true,
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () => import("../pages/HumanResource/EmployeeGroup/EmployeeGroupList.vue"),
              meta: {
                requiredPermissions: ["EmployeeGroup.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/HumanResource/EmployeeGroup/EmployeeGroupNew.vue"),
              meta: {
                requiredPermissions: ["EmployeeGroup.Create"]
              }
            },
            {
              path: "detail/:id",
              component: () =>
                import("../pages/HumanResource/EmployeeGroup/EmployeeGroupDetail.vue"),
              meta: {
                requiredPermissions: ["EmployeeGroup.View"]
              }
            }
          ]
        },
        {
          path: "training-type",
          meta: { menuName: "Training" },
          children: [
            {
              path: "",
              component: () => import("../pages/TrainingTypeManagement.vue"),
              meta: {
                requiredPermissions: ["Training.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/TrainingTypeNew.vue"),
              meta: {
                requiredPermissions: ["Training.Create"]
              }
            },
            {
              path: ":id",
              component: () => import("../pages/TrainingTypeDetail.vue"),
              meta: {
                requiredPermissions: ["Training.View"]
              }
            }
          ]
        },
        {
          path: "training-course",
          meta: { menuName: "Training Course" },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/HumanResource/TrainingCourse/TrainingCourseList.vue"),
              meta: {
                requiredPermissions: ["Training.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/HumanResource/TrainingCourse/TrainingCourseNew.vue"),
              meta: {
                requiredPermissions: ["Training.Create"]
              }
            },
            {
              path: "detail/:id",
              component: () =>
                import("../pages/HumanResource/TrainingCourse/TrainingCourseDetail.vue"),
              meta: {
                requiredPermissions: ["Training.View"]
              }
            }
          ]
        },
        {
          path: "training-provider",
          meta: { menuName: "Training Provider" },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/HumanResource/TrainingProvider/TrainingProviderList.vue"),
              meta: {
                requiredPermissions: ["TrainingProvider.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/HumanResource/TrainingProvider/TrainingProviderNew.vue"),
              meta: {
                requiredPermissions: ["TrainingProvider.Create"]
              }
            },
            {
              path: "detail/:id",
              component: () =>
                import("../pages/HumanResource/TrainingProvider/TrainingProviderDetail.vue"),
              meta: {
                requiredPermissions: ["TrainingProvider.View"]
              }
            }
          ]
        },
        {
          path: "training",
          meta: { menuName: "Training" },
          children: [
            {
              path: "",
              component: () => import("../pages/HumanResource/Training/TrainingList.vue"),
              meta: {
                requiredPermissions: ["Training.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/HumanResource/Training/TrainingNew.vue"),
              meta: {
                requiredPermissions: ["Training.Create"]
              }
            },
            {
              path: "detail/:id",
              component: () => import("../pages/HumanResource/Training/TrainingDetail.vue"),
              meta: {
                requiredPermissions: ["Training.View"]
              }
            }
          ]
        },
        {
          path: "employee-training",
          meta: {
            menuName: "Employee Training",
            requireLogin: true
          },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/HumanResource/EmployeeTraining/EmployeeTrainingList.vue"),
              meta: {
                requiredPermissions: ["EmployeeTraining.View"]
              }
            }
          ]
        },
        // employee training v2 starts here
        {
          path: "employee-training-v2",
          meta: {
            menuName: "Employee Training V2",
            requireLogin: true
          },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/HumanResource/EmployeeTraining/EmployeeTrainingListV2.vue"),
              meta: {
                requiredPermissions: ["EmployeeTraining.View"]
              }
            },
            {
              path: "detail/:employeeId/:trainingId/:id",
              props: true,
              component: () =>
                import("../pages/HumanResource/EmployeeTraining/EmployeeTraingingDetail.vue"),
              meta: {
                // requiredPermissions: ["Employee.View"]
                requiredPermissions: ["EmployeeTraining.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/HumanResource/EmployeeTraining/EmployeeTrainingNew.vue"),
              meta: {
                requiredPermissions: ["EmployeeTraining.Create"]
              }
            }
          ]
        },
        // employee training v2 ends here
        {
          path: "employee-vaccination",
          component: () =>
            import("../pages/HumanResource/EmployeeVaccination/EmployeeVaccinationList.vue"),
          meta: {
            menuName: "Employee Vaccination",
            requireLogin: true,
            requiredPermissions: ["Vaccination.View"]
          }
        },
        {
          path: "medical-check-up",
          component: () =>
            import("../pages/HumanResource/MedicalCheckUp/EmployeeMedicalCheckup.vue"),
          meta: {
            menuName: "Medical Check-up",
            requireLogin: true,
            requiredPermissions: ["Medical.View"]
          }
        },
        {
          path: "medical-check-up-type",
          meta: {
            menuName: "Medical Check-up",
            requireLogin: true,
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () => import("../pages/MedicalCheckupTypeManagement.vue"),
              meta: {
                requiredPermissions: ["MedicalType.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/MedicalCheckupTypeNew.vue"),
              meta: {
                requiredPermissions: ["MedicalType.Create"]
              }
            },
            {
              path: ":id",
              component: () => import("../pages/MedicalCheckupTypeDetail.vue"),
              meta: {
                requiredPermissions: ["MedicalType.View"]
              }
            }
          ]
        },
        {
          path: "typhoid-vaccination-entry",
          meta: {
            menuName: "Typhoid Vaccination",
            requireLogin: true,
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/HumanResource/TyphoidVaccination/TyphoidVaccinationList.vue"),
              meta: {
                requiredPermissions: ["TyphoidVaccination.View"]
              }
            },
            {
              path: "new",
              component: () =>
                import("../pages/HumanResource/TyphoidVaccination/TyphoidVaccinationNew.vue"),
              meta: {
                requiredPermissions: ["TyphoidVaccination.Create"]
              }
            },
            {
              path: "detail/:id",
              component: () =>
                import("../pages/HumanResource/TyphoidVaccination/TyphoidVaccinationDetail.vue"),
              meta: {
                requiredPermissions: ["TyphoidVaccination.View"]
              }
            }
          ]
        },
        {
          path: "vaccination-type",
          meta: {
            menuName: "Employee Vaccination",
            requireLogin: true,
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () => import("../pages/VaccinationTypeManagement.vue"),
              meta: {
                requiredPermissions: ["VaccinationType.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/VaccinationTypeNew.vue"),
              meta: {
                requiredPermissions: ["VaccinationType.Create"]
              }
            },
            {
              path: ":id",
              component: () => import("../pages/VaccinationTypeDetail.vue"),
              meta: {
                requiredPermissions: ["VaccinationType.View"]
              }
            }
          ]
        }
      ]
    },
    {
      path: "/complaint",
      meta: {
        requireLogin: true,
        requiredPermissions: ["Complaint.View"]
      },
      children: [
        {
          path: "",
          component: () => import("../pages/Complaint/ComplaintList.vue"),
          meta: {
            requiredPermissions: ["Complaint.View"]
          }
        },
        {
          path: "new",
          component: () => import("../pages/Complaint/ComplaintNew.vue"),
          meta: {
            requiredPermissions: ["Complaint.Create"]
          }
        },
        {
          path: "detail/:id",
          component: () => import("../pages/Complaint/ComplaintEdit.vue"),
          meta: {
            requiredPermissions: ["Complaint.View"]
          }
        },
        {
          path: "detail/:id/view",
          component: () => import("../pages/Complaint/ComplaintView.vue"),
          meta: {
            requiredPermissions: ["Complaint.View"]
          }
        },
        {
          path: "resolve/:id",
          component: () => import("../pages/Complaint/ComplaintResolve.vue"),
          meta: {
            requiredPermissions: ["Complaint.View"]
          }
        },
        {
          path: "close/:id",
          component: () => import("../pages/Complaint/ComplaintClose.vue"),
          meta: {
            requiredPermissions: ["Complaint.View"]
          }
        }
      ]
    },
    {
      path: "/customer-voice",
      meta: {
        requireLogin: true,
        requiredPermissions: ["Complaint.View"]
      },
      children: [
        {
          path: "",
          component: () => import("../pages/CustomerVoice/ComplaintList.vue"),
          meta: {
            requiredPermissions: ["Complaint.View"]
          }
        },
        {
          path: "new",
          component: () => import("../pages/CustomerVoice/ComplaintNew.vue"),
          meta: {
            requiredPermissions: ["Complaint.Create"]
          }
        },
        {
          path: "detail/:id",
          component: () => import("../pages/CustomerVoice/ComplaintEdit.vue"),
          meta: {
            requiredPermissions: ["Complaint.View"]
          }
        },
        {
          path: "detail/:id/view",
          component: () => import("../pages/CustomerVoice/ComplaintView.vue"),
          meta: {
            requiredPermissions: ["Complaint.View"]
          }
        },
        {
          path: "resolve/:id",
          component: () => import("../pages/CustomerVoice/ComplaintResolve.vue"),
          meta: {
            requiredPermissions: ["Complaint.View"]
          }
        },
        {
          path: "close/:id",
          component: () => import("../pages/CustomerVoice/ComplaintClose.vue"),
          meta: {
            requiredPermissions: ["Complaint.View"]
          }
        }
      ]
    },
    {
      path: "/user",
      meta: {
        requireLogin: true,
        requiredPermissions: []
      },
      children: [
        {
          path: "",
          component: () => import("../pages/UserManagement/UserManagement.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["UserManagement.View", "User.View"]
          }
        },
        {
          path: "new",
          component: () => import("../pages/UserManagement/UserNew.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["UserManagement.Create", "User.View"]
          }
        },
        {
          path: "detail/:id",
          component: () => import("../pages/UserManagement/UserDetail.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["UserManagement.View", "User.View"]
          }
        },
        {
          path: "organization-detail/:id",
          component: () => import("../pages/UserManagement/UserDetailOrganization.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["UserManagement.View", "User.View"]
          }
        },
        {
          path: "detail/:id/resetpassword",
          component: () => import("../pages/UserManagement/UserPassword.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["UserManagement.View", "User.View"]
          }
        },
        {
          path: "profile",
          component: () => import("../pages/UserManagement/UserProfile.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["Dashboard.View"]
          }
        }
      ]
    },
    {
      path: "/user-confirmation/:id",
      component: () => import("../pages/UserConfirmation.vue"),
      meta: {
        requireLogin: false,
        requiredPermissions: []
      }
    },
    {
      path: "/organization",
      meta: {
        menuName: "Organization Management",
        requireLogin: true,
        requiredPermissions: []
      },
      children: [
        {
          path: "management",
          component: () =>
            import("../pages/Organization/OrganizationManagement/OrganizationManagement.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["OrganizationManagement.View"]
          }
        },
        {
          path: "new",
          component: () =>
            import("../pages/Organization/OrganizationManagement/OrganizationNew.vue"),
          meta: {
            requireLogin: true,
            requiredPermissions: ["OrganizationManagement.Create"]
          }
        },
        {
          path: "detail/:id",
          component: () =>
            import("../pages/Organization/OrganizationManagement/OrganizationDetail.vue"),
          meta: {
            menuName: "Organization Detail",
            requireLogin: true,
            requiredPermissions: ["Organization.View"]
          }
        },
        {
          path: "assign-menu/:id",
          component: () =>
            import("../pages/Organization/OrganizationManagement/OrganizationAssignMenu.vue"),
          meta: {
            menuName: "Assign Menu",
            requireLogin: true,
            requiredPermissions: ["Organization.View"]
          }
        },
        {
          path: "site",
          meta: {
            menuName: "Sites",
            requireLogin: true,
            requiredPermissions: ["Site.View"]
          },
          children: [
            {
              path: "",
              component: () => import("../pages/Organization/Sites/SiteList.vue"),
              meta: {
                requireLogin: true,
                requiredPermissions: ["Site.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/Organization/Sites/SiteNew.vue"),
              meta: {
                requireLogin: true,
                requiredPermissions: ["Site.Create"]
              }
            },
            {
              path: "detail/:id",
              component: () => import("../pages/Organization/Sites/SiteDetail.vue"),
              meta: {
                requireLogin: true,
                requiredPermissions: ["Site.View"]
              }
            }
          ]
        }
      ]
    },
    {
      path: "/notifications",
      component: () => import("../pages/Notifications/NotificationsList.vue"),
      meta: {
        requireLogin: true,
        requiredPermissions: []
      }
    },
    {
      path: "/role-access",
      meta: {
        requireLogin: true
      },
      children: [
        {
          path: "",
          component: () => import("../pages/RoleAccess/RoleAccessList.vue"),
          meta: {
            menuName: "Roles Access",
            requiredPermissions: []
          }
        }
      ]
    },
    {
      path: "/systemlog",
      meta: { requireLogin: true },
      children: [
        {
          path: "emailhistory",
          component: () => import("../pages/SystemLog/EmailHistory/EmailHistoryList.vue"),
          meta: {
            menuName: "System Log",
            requiredPermissions: []
          }
        },
        {
          path: "user-action-log",
          component: () => import("../pages/SystemLog/UserActionLog/UserActionLogPage.vue"),
          meta: {
            menuName: "User Action Log",
            requiredPermissions: []
          }
        },
        {
          path: "halal-file-action-log",
          component: () =>
            import("../pages/SystemLog/HalalFileValidationLog/HalalFileValidationLogPage.vue"),
          meta: {
            menuName: "Halal File Validation Action Log",
            requiredPermissions: []
          }
        }
      ]
    },
    {
      path: "/system-config",
      meta: { requireLogin: true },
      children: [
        {
          path: "certification-body",
          meta: {
            menuName: "Certificate Body",
            requireLogin: true
          },
          children: [
            {
              path: "",
              component: () => import("../pages/Operation/CertificateBody/CertBodyList.vue")
            },
            {
              path: "detail/:id",
              component: () => import("../pages/Operation/CertificateBody/CertBodyDetails.vue"),
              meta: {
                requiredPermissions: ["CertificationBody.Edit"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/Operation/CertificateBody/CertBodyNew.vue"),
              meta: {
                requiredPermissions: ["CertificationBody.Create"]
              }
            }
          ]
        },
        {
          path: "country",
          meta: {
            menuName: "Country",
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () => import("../pages/SystemConfig/Country/CountryList.vue")
            },
            {
              path: "detail/:id",
              component: () => import("../pages/SystemConfig/Country/CountryDetails.vue"),
              meta: {
                title: "Edit Country"
              }
            },
            {
              path: "new",
              component: () => import("../pages/SystemConfig/Country/CountryNew.vue"),
              meta: {
                title: "New Country"
              }
            }
          ]
        },
        {
          path: "organization-package",
          // component: () => import("../pages/SystemConfig/OrganizationPackageList.vue"),
          meta: {
            menuName: "Organization Package",
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () => import("../pages/SystemConfig/OrganizationPackageList.vue"),
              meta: {
                requiredPermissions: ["OrganizationPackage.View"]
              }
            },
            {
              path: "new",
              component: () => import("../pages/SystemConfig/OrganizationPackageNew.vue"),
              meta: {
                requiredPermissions: ["OrganizationPackage.Create"]
              }
            },
            {
              path: "detail/:id",
              component: () => import("../pages/SystemConfig/OrganizationPackageDetail.vue"),
              meta: {
                requiredPermissions: ["OrganizationPackage.Edit"]
              }
            },
            {
              path: "assign-menu/:id",
              component: () => import("../pages/SystemConfig/OrganizationPackageAssignMenu.vue"),
              meta: {
                requiredPermissions: ["OrganizationPackage.Edit"]
              }
            }
          ]
        },
        {
          path: "industry-certification-name",
          meta: {
            menuName: "Industry Certification Name",
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () =>
                import(
                  "../pages/SystemConfig/IndustryCertificationName/IndustryCertificationNameManagement.vue"
                )
            },
            {
              path: "new",
              component: () =>
                import(
                  "../pages/SystemConfig/IndustryCertificationName/IndustryCertificationNameForm.vue"
                ),
              meta: { title: "New Industry Certification Name" }
            },
            {
              path: ":id",
              component: () =>
                import(
                  "../pages/SystemConfig/IndustryCertificationName/IndustryCertificationNameForm.vue"
                ),
              meta: { title: "Edit Industry Certification Name" }
            }
          ]
        }
      ]
    },
    {
      path: "/collaborator",
      meta: { requireLogin: true },
      children: [
        {
          path: "supplier-connection",
          component: () =>
            import("../pages/Collaborator/SupplierConnected/SupplierConnectionList.vue"),
          meta: {
            menuName: "Connection Request",
            requiredPermissions: ["SupplierConnection.View"]
          }
        },
        {
          path: "buyer-connection",
          component: () => import("../pages/Collaborator/BuyerConnected/BuyerConnectionList.vue"),
          meta: {
            menuName: "Connection Request",
            requiredPermissions: ["BuyerConnection.View"]
          }
        }
      ]
    },
    {
      path: "/supplier-confirmation/:hashCode",
      component: () => import("../pages/NonUserForm/SupplierActivation.vue"),
      meta: {
        requireLogin: false,
        requiredPermissions: []
      }
    },
    {
      path: "/notification-management",
      meta: { requireLogin: true },
      children: [
        {
          path: "notification-setting",
          component: () => import("../pages/NotificationManagement/NotificationSetting.vue"),
          meta: {
            menuName: "Notification Setting",
            requiredPermissions: []
          }
        },
        {
          path: "notification-setting-histories",
          component: () =>
            import("../pages/NotificationManagement/NotificationSettingHistories.vue"),
          meta: {
            menuName: "Notification Histories",
            requiredPermissions: []
          }
        },
        {
          path: "manage-notification-template",
          meta: {
            menuName: "Notification Template",
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () => import("../pages/NotificationTemplate/NotificationTemplate.vue")
            },
            {
              path: "new",
              component: () => import("../pages/NotificationTemplate/NotificationTemplateNew.vue"),
              meta: {
                requiredPermissions: []
              }
            },
            {
              path: "detail/:id",
              component: () => import("../pages/NotificationTemplate/NotificationTemplateEdit.vue"),
              meta: {
                requiredPermissions: []
              }
            }
          ]
        }
      ]
    },
    {
      path: "/admin-settings",
      meta: {
        requireLogin: true,
        requiredPermissions: ["AdminSettingMenu.View"]
      },
      children: [
        {
          path: "auto-suspension",
          component: () => import("../pages/AdminSettings/AutoSuspension.vue"),
          meta: {
            requiredPermissions: ["AutoSuspensionSetting.View", "AutoSuspensionSetting.Edit"]
          }
        },
        {
          path: "notification-setting",
          component: () => import("../pages/AdminSettings/NotificationSetting.vue"),
          meta: {
            menuName: "Notification Setting",
            requiredPermissions: []
          }
        },
        {
          path: "audit-workflow",
          component: () => import("../pages/AdminSettings/AuditWorkflow.vue"),
          meta: {
            requiredPermissions: ["AuditWorkflowSetting.View"]
          }
        },
        {
          path: "employee-setting",
          component: () => import("../pages/AdminSettings/EmployeeNumSetting.vue"),
          meta: {
            requiredPermissions: ["EmployeeSetting.View"]
          }
        },
        {
          path: "role-access",
          meta: {
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () => import("../pages/RoleAccess/RoleAccessListV2.vue"),
              meta: {
                requiredPermissions: ["OrganizationRoleAccess.View"]
              }
            },
            {
              path: "detail/:id",
              component: () => import("../pages/RoleAccess/RoleAccessDetail.vue"),
              meta: {
                requiredPermissions: ["OrganizationRoleAccess.View"]
              }
            }
          ]
        },
        {
          path: "organization-package-detail",
          component: () => import("../pages/AdminSettings/OrganizationPackageDetail.vue"),
          meta: {
            requiredPermissions: []
          }
        },
        {
          path: "industry-certification-name",
          meta: {
            menuName: "Industry Certification Name",
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () =>
                import(
                  "../pages/AdminSettings/IndustryCertificationName/IndustryCertificationNameManagement.vue"
                )
            },
            {
              path: "new",
              component: () =>
                import(
                  "../pages/AdminSettings/IndustryCertificationName/IndustryCertificationNameForm.vue"
                ),
              meta: { title: "New Industry Certification Name" }
            },
            {
              path: ":id",
              component: () =>
                import(
                  "../pages/AdminSettings/IndustryCertificationName/IndustryCertificationNameForm.vue"
                ),
              meta: { title: "Edit Industry Certification Name" }
            }
          ]
        },
        {
          path: "industry-certification-body",
          meta: {
            menuName: "Industry Certification Body",
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () =>
                import(
                  "../pages/SystemConfig/IndustryCertificationBody/IndustryCertificationBodyManagement.vue"
                )
            },
            {
              path: "new",
              component: () =>
                import(
                  "../pages/SystemConfig/IndustryCertificationBody/IndustryCertificationBodyForm.vue"
                ),
              meta: { title: "New Industry Certification Body" }
            },
            {
              path: ":id",
              component: () =>
                import(
                  "../pages/SystemConfig/IndustryCertificationBody/IndustryCertificationBodyForm.vue"
                ),
              meta: { title: "Edit Industry Certification Body" }
            }
          ]
        },
        {
          path: "auto-certificate-request-setting",
          component: () => import("../pages/AdminSettings/AutoCertificateRequestSetting.vue"),
          meta: {
            menuName: "Auto Certificate Request Setting",
            requiredPermissions: []
          }
        },
        {
          path: "pest-control-method",
          meta: {
            menuName: "Pest Control Method",
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () =>
                import("../pages/AdminSettings/PestControlMethod/PestControlMethodManagement.vue")
            },
            {
              path: "new",
              component: () =>
                import("../pages/AdminSettings/PestControlMethod/PestControlMethodForm.vue"),
              meta: { title: "New Pest Control Method" }
            },
            {
              path: ":id",
              component: () =>
                import("../pages/AdminSettings/PestControlMethod/PestControlMethodForm.vue"),
              meta: { title: "Edit Pest Control Method" }
            }
          ]
        },
        {
          path: "sensor-management",
          component: () => import("../pages/AdminSettings/SensorManagement/SensorManagement.vue"),
          meta: {
            menuName: "Sensor Management"
          }
        },
        {
          path: "pest-control-site-area",
          meta: {
            menuName: "Pest Control Site Area",
            requiredPermissions: []
          },
          children: [
            {
              path: "",
              component: () =>
                import(
                  "../pages/AdminSettings/PestControlSiteArea/PestControlSiteAreaManagement.vue"
                )
            },
            {
              path: "new",
              component: () =>
                import("../pages/AdminSettings/PestControlSiteArea/PestControlSiteAreaForm.vue"),
              meta: { title: "New Pest Control Site Area" }
            },
            {
              path: ":id",
              component: () =>
                import("../pages/AdminSettings/PestControlSiteArea/PestControlSiteAreaForm.vue"),
              meta: { title: "Edit Pest Control Site Area" }
            }
          ]
        }
      ]
    },
    {
      path: "/not-found",
      component: () => import("../pages/NotFound.vue"),
      meta: {
        requireLogin: false,
        requiredPermissions: []
      }
    }
  ]
});

router.beforeEach(async (toRoute: RouteLocationNormalized) => {
  try {
    const currentUserStore = useCurrentUserStore();
    const permissionStore = usePermissionStore();

    //console.log('usePermissionStore: ', permissionStore.permissions);

    const { requireLogin, requiredPermissions } = toRoute.meta;
    const notLoggedIn = Boolean(currentUserStore.getCurrentUser.value === null);

    /* when user is not logged in */
    if (requireLogin && notLoggedIn) {
      return { path: "/login" };
    }

    /* no permission needed */
    if (!requiredPermissions || requiredPermissions.length === 0) {
      /**
       * Note:
       *
       * Empty requiredPermissions array falls into this condition.
       */
      return true;
    }

    const canAccess = permissionStore.hasPermission(...requiredPermissions);
    //console.log("canAccess", canAccess);
    //console.log("requiredPermissions", requiredPermissions);
    return canAccess ? true : { path: "/not-found" };
  } catch (error: unknown) {
    console.error("Router error", error);
    return { path: "/not-found" };
  }
});

export default router;
