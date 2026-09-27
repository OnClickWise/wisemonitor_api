namespace WiseMonitor.Api.Models.Billing
{
    /// <summary>
    /// Feature entitlements comercializáveis. O plano (e os add-ons) concedem features;
    /// endpoints e telas verificam a feature, nunca o nome do plano.
    /// </summary>
    public static class Features
    {
        // Starter
        public const string Dashboard           = "DASHBOARD";
        public const string TimeTracking        = "TIME_TRACKING";
        public const string ActivityTracking    = "ACTIVITY_TRACKING";
        public const string AppTracking         = "APP_TRACKING";
        public const string Teams               = "TEAMS";
        public const string BasicProductivity   = "BASIC_PRODUCTIVITY";
        public const string BasicReports        = "BASIC_REPORTS";

        // Professional
        public const string Screenshots           = "SCREENSHOTS";
        public const string ScreenshotInterval    = "SCREENSHOT_INTERVAL";
        public const string LiveStream            = "LIVE_STREAM";
        public const string UrlTracking           = "URL_TRACKING";
        public const string Projects              = "PROJECTS";
        public const string Tasks                 = "TASKS";
        public const string ProductivityAnalytics = "PRODUCTIVITY_ANALYTICS";
        public const string FullReports           = "FULL_REPORTS";
        public const string ReportsExport         = "REPORTS_EXPORT";

        // Business
        public const string Departments          = "DEPARTMENTS";
        public const string Alerts               = "ALERTS";
        public const string MonitoringPolicies   = "MONITORING_POLICIES";
        public const string ExecutiveAnalytics   = "EXECUTIVE_ANALYTICS";
        public const string AdvancedReports      = "ADVANCED_REPORTS";
        public const string ScheduledReports     = "SCHEDULED_REPORTS";
        public const string ApiAccess            = "API_ACCESS";
        public const string Webhooks             = "WEBHOOKS";

        // Enterprise
        public const string Sso                  = "SSO";
        public const string AdvancedAudit        = "ADVANCED_AUDIT";
        public const string CustomRetention      = "CUSTOM_RETENTION";
        public const string CustomReports        = "CUSTOM_REPORTS";
        public const string EnterpriseSecurity   = "ENTERPRISE_SECURITY";
        public const string Sla                  = "SLA";
        public const string DedicatedSupport     = "DEDICATED_SUPPORT";

        // Add-ons
        public const string AiInsights           = "AI_INSIGHTS";
        public const string Dlp                  = "DLP";
        public const string PremiumSupport       = "PREMIUM_SUPPORT";
    }
}
