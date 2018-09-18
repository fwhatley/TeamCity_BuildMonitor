using BuildMonitor.Models.Home;
using Newtonsoft.Json;

namespace BuildMonitor.Helpers
{
    public class DefaultBuildMonitorModelHandler : BuildMonitorModelHandlerBase
	{
		public override BuildMonitorViewModel GetModel()
		{
			var model = new BuildMonitorViewModel();

			GetTeamCityBuildsJson();

			var count = (int)projectsJson.count;
			for (int i = 0; i < count; i++)
			{
				var project = new Project();
				var projectJson = projectsJson.project[i];

				project.Id = projectJson.id;
				project.Name = projectJson.name;

			    if (isProjectManagedByFcTeam(project.Name))
			    {
			        AddBuilds(ref project);
			        model.Projects.Add(project);
                }

			}

			return model;
		}

	    private bool isProjectManagedByFcTeam(string projectName)
	    {
	        var FcUiModulesProjectPrefix = "UI Module EC";
	        var FcSeleniumAutomationProjectPrefix = "UI Automation Test";
	        var FcApiTestingProject = "API Testing"; // TODO: figure out why automation tests are not coming back in the projects array list
	        var msvcFcProjectPrefix = "msvc-";  // TODO: pull down all services. Right now there is null ref exception if we try to pull all services
	        var sandFcProjectPrefix = "sand-";
	        var utilFcProjectPrefix = "Util-";


            // only add builds for FC
            if (projectName.Contains(FcUiModulesProjectPrefix) ||
	            projectName.Contains(FcSeleniumAutomationProjectPrefix) ||
                projectName.Contains(FcApiTestingProject) )
	        {

	            var AbsTimeSelenumAutomationProjectPrefix = "UI Automation Testing";
                if (projectName.Contains(AbsTimeSelenumAutomationProjectPrefix))
	            {
	                return false; // don't track AbsTime project labeled similar to FC
	            }


	            return true;
	        }

	        return false;
	    }

		private void AddBuilds(ref Project project)
		{
			var count = (int)buildTypesJson.count;
			for (int i = 0; i < count; i++)
			{
				var buildTypeJson = buildTypesJson.buildType[i];

				if (buildTypeJson.projectId != project.Id)
				{
					continue;
				}

				var build = new Build();
				build.Id = buildTypeJson.id;
				build.Name = buildTypeJson.name;

				var url = string.Format(buildStatusUrl, build.Id);
				var buildStatusJsonString = RequestHelper.GetJson(url);
				buildStatusJson = JsonConvert.DeserializeObject<dynamic>(buildStatusJsonString ?? string.Empty);

                build.Branch = (buildStatusJson != null) ? (buildStatusJson.branchName ?? "default") : "unknown";
                build.Status = GetBuildStatusForRunningBuild(build.Id);

				if (build.Status == BuildStatus.Running)
				{
					UpdateBuildStatusFromRunningBuildJson(build.Id);
				}

				build.UpdatedBy = GetUpdatedBy();
				build.LastRunText = GetLastRunText();
				build.IsQueued = IsBuildQueued(build.Id);
				build.StatusDescription = (string)buildStatusJson.statusText;

				if (build.Status == BuildStatus.Running)
				{
					var result = GetRunningBuildBranchAndProgress(build.Id);
					build.Branch = result[0];
					build.Progress = result[1];
				}
				else
				{
					build.Progress = string.Empty;
				}

				project.Builds.Add(build);
			}
		}

		private bool IsBuildQueued(string buildId)
		{
			try
			{
				var count = (int)buildQueueJson.count;
				for (int i = 0; i < count; i++)
				{
					var build = buildQueueJson.build[i];

					if (buildId == (string)build.buildTypeId && (string)build.state == "queued")
					{
						return true;
					}
				}
			}
			catch
			{
			}

			return false;
		}

		private string GetUpdatedBy()
		{
			try
			{
				var triggerType = (string)buildStatusJson.triggered.type;
                if (triggerType == "user")
				{
					return (string)buildStatusJson.triggered.user.name;
				}

				if (triggerType == "vcs" && buildStatusJson.lastChanges != null)
				{
					var result = RequestHelper.GetJson(teamCityUrl + buildStatusJson.lastChanges.change[0].href);
					var change = JsonConvert.DeserializeObject<dynamic>(result);

					return (string)change.user.name;
				}

				if (triggerType == "unknown")
				{
					return "TeamCity";
				}
			}
			catch
			{
			}

			return "Unknown";
		}
	}
}