using Sitecore.Configuration;
using Sitecore.Diagnostics;
using Sitecore.Pipelines;
using System;

namespace SiteKit
{
    public static class AutoManager
    {
        public static string Run(string siteName, AutoManagerActions action)
        {
            string log = "";
            try
            {
                // First run validation pipeline
                var validationArgs = new AutoArgs(siteName);
                CorePipeline.Run("validateYamlData", validationArgs);

                // Check if validation passed
                if (validationArgs.IsValid)
                {
                    // If validation passed, run build pipeline
                    if (action == AutoManagerActions.ValidateAndDeploy)
                        CorePipeline.Run("buildItems", new AutoArgs(siteName));
                    log = action == AutoManagerActions.ValidateAndDeploy ? "ok;" : "valid;";
                }
                else
                {
                    // If validation failed, return the validation message
                    log = validationArgs.ValidationMessage;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex, ex);
                log = ex.Message + Environment.NewLine + ex.InnerException?.Message;
                log += Environment.NewLine;
                log += Environment.NewLine;
                log += "Stack Trace:";
                log += Environment.NewLine;
                log += ex.StackTrace;
            }

            var folder = Factory.GetDatabase("master").GetItem("/sitecore/system/Modules/SiteKit/" + siteName);
            folder.Editing.BeginEdit();
            folder["Log"] = log;
            folder.Editing.EndEdit();

            return log;
        }
    }

    public enum AutoManagerActions
    {
        ValidateAndDeploy,
        Validate
    }
}
