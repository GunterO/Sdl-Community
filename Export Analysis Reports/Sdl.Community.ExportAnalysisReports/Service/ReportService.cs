﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Newtonsoft.Json;
using Sdl.Community.ExportAnalysisReports.Helpers;
using Sdl.Community.ExportAnalysisReports.Interfaces;
using Sdl.Community.ExportAnalysisReports.Model;

namespace Sdl.Community.ExportAnalysisReports.Service
{
	public class ReportService : IReportService
	{
		private readonly IMessageBoxService _messageBoxService;
		private readonly IStudioService _studioService;
		private readonly string _communityFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SDL Community", "ExportAnalysisReports");
		private Help _help;

		public static readonly Log Log = Log.Instance;
		public string JsonPath => Path.Combine(_communityFolderPath, "ExportAnalysisReportSettings.json");
		public string ReportsFolderPath { get; set; }

		public ReportService(IMessageBoxService messageBoxService, IStudioService studioService)
		{
			_help = new Help();
			_messageBoxService = messageBoxService;
			_studioService = studioService;
		}

		public void LoadReports(XmlDocument doc, ProjectDetails project)
		{
			try
			{
				var projectInfo = _studioService.GetProjectInfo(project.ProjectPath);

				project.LanguageAnalysisReportPaths?.Clear();

				var automaticTaskNode = doc.SelectNodes("/Project/Tasks/AutomaticTask");
				if (automaticTaskNode != null)
				{
					var reportsFolderExist = ReportsFolderExists(project.ReportsFolderPath);
					if (reportsFolderExist)
					{
						foreach (var node in automaticTaskNode)
						{
							var task = (XmlNode)node;
							var reportNodes = task.SelectNodes("Reports/Report");

							if (reportNodes == null) continue;

							foreach (var reportNode in reportNodes)
							{
								var report = (XmlNode)reportNode;
								if (report.Attributes != null && report.Attributes["TaskTemplateId"].Value.Equals("Sdl.ProjectApi.AutomaticTasks.Analysis"))
								{
									var reportLangDirectionId = report.Attributes["LanguageDirectionGuid"].Value;
									var languageDirectionsNode = doc.SelectNodes("Project/LanguageDirections/LanguageDirection");
									foreach (XmlNode langDirNode in languageDirectionsNode)
									{
										var fileLangGuid = langDirNode.Attributes["Guid"].Value;
										if (reportLangDirectionId.Equals(fileLangGuid))
										{
											var targetLangCode = langDirNode.Attributes["TargetLanguageCode"].Value;
											var langName = projectInfo?.TargetLanguages?.FirstOrDefault(n => n.IsoAbbreviation.Equals(targetLangCode));

											var reportPath = Path.Combine(project.ProjectFolderPath, report.Attributes["PhysicalPath"].Value);
											if (!string.IsNullOrEmpty(project.ReportsFolderPath))
											{
												reportPath = Path.Combine(project.ReportsFolderPath, Path.GetFileName(report.Attributes["PhysicalPath"].Value));
											}
											if (project.LanguageAnalysisReportPaths == null)
											{
												project.LanguageAnalysisReportPaths = new Dictionary<string, string>();
											}
											project.LanguageAnalysisReportPaths.Add(langName?.DisplayName, reportPath);
										}
									}
								}
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Logger.Error($"LoadReports method: {ex.Message}\n {ex.StackTrace}");
			}
		}

		public string GetJsonReportPath(string jsonPath)
		{
			if (File.Exists(jsonPath))
			{
				using (var r = new StreamReader(jsonPath))
				{
					var json = r.ReadToEnd();
					var item = JsonConvert.DeserializeObject<JsonSettings>(json);
					if (!string.IsNullOrEmpty(item.ExportPath))
					{
						return item.ExportPath;
					}
				}
			}
			return string.Empty;
		}

		public Dictionary<string, LanguageDirection> LoadLanguageDirections(XmlDocument doc)
		{
			var languages = new Dictionary<string, LanguageDirection>();
			try
			{
				var languagesDirectionNode = doc.SelectNodes("/Project/LanguageDirections/LanguageDirection");

				if (languagesDirectionNode == null) return languages;
				foreach (var item in languagesDirectionNode)
				{
					var node = (XmlNode)item;
					if (node.Attributes == null) continue;
					var lang = new LanguageDirection
					{
						Guid = node.Attributes["Guid"].Value,
						TargetLang = CultureInfo.GetCultureInfo(node.Attributes["TargetLanguageCode"].Value)
					};
					if (!languages.ContainsKey(lang.Guid))
					{
						languages.Add(lang.Guid, lang);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Logger.Error($"LoadLanguageDirections method: {ex.Message}\n {ex.StackTrace}");
			}
			return languages;
		}

		/// <summary>
		/// Check if the exported path is the same
		/// </summary>
		/// <param name="reportOutputPath"></param>
		/// <returns></returns>
		public bool IsSameReportPath(string reportOutputPath)
		{
			var jsonReportPath = GetJsonReportPath(JsonPath);
			if (!string.IsNullOrEmpty(jsonReportPath) && !string.IsNullOrEmpty(reportOutputPath) && jsonReportPath.Equals(reportOutputPath))
			{
				return true;
			}

			return false;
		}

		public bool ReportFileExist(string reportFolderPath)
		{
			try
			{
				if (!string.IsNullOrEmpty(reportFolderPath))
				{
					var fileName = Path.GetFileName(Path.GetDirectoryName(reportFolderPath));
					if (Directory.Exists(reportFolderPath))
					{
						var files = Directory.GetFiles(reportFolderPath);
						if (files.Any(file => file.Contains("Analyze Files")))
						{
							return true;
						}

						_messageBoxService.ShowInformationMessage(string.Format(PluginResources.ExecuteAnalyzeBatchTask_Message, fileName), PluginResources.InformativeLabel);
						return false;
					}

					if (!string.IsNullOrEmpty(fileName) && fileName.Contains("ProjectFiles"))
					{
						fileName = Path.GetFileNameWithoutExtension(fileName);
						return !string.IsNullOrEmpty(fileName);
					}

					_messageBoxService.ShowInformationMessage(string.Format(PluginResources.ExecuteAnalyzeBatchTask_Message, fileName), PluginResources.InformativeLabel);
				}

				return false;
			}
			catch (Exception ex)
			{
				Log.Logger.Error($"ReportFileExist method: {ex.Message}\n {ex.StackTrace}");
			}
			return false;
		}

		/// <summary>
		/// Check if the report folder exists
		/// </summary>
		/// <param name="projectInfoNode"></param>
		/// <param name="projectXmlPath"></param>
		/// <returns></returns>
		public bool ReportFolderExist(XmlNode projectInfoNode, string projectXmlPath)
		{
			try
			{
				if (projectInfoNode?.Attributes != null)
				{
					var filePath = string.Empty;

					if (projectInfoNode.Attributes["ProjectFilePath"] != null)
					{
						filePath = projectInfoNode.Attributes["ProjectFilePath"]?.Value;
						if (!Path.IsPathRooted(filePath))
						{
							//project is located inside "Projects" folder in Studio
							var projectsFolderPath = Path.GetDirectoryName(projectXmlPath);
							// .Substring is needed here to keep the selection of projectNamePath correctly, ex: Samples\Sample Project
							var projectNamePath = filePath.Substring(0, filePath.LastIndexOf(@"\", StringComparison.Ordinal));
							if (!string.IsNullOrEmpty(projectsFolderPath) && !string.IsNullOrEmpty(projectNamePath))
							{
								filePath = Path.Combine(projectsFolderPath, projectNamePath, "Reports");
							}
						}
						else
						{
							// is external or single file project
							var reportsPath = Path.GetDirectoryName(filePath);
							if (!string.IsNullOrEmpty(reportsPath))
							{
								filePath = Path.Combine(reportsPath, "Reports");
								if (!Directory.Exists(filePath))
								{
									// get the single file project Reports folder's path
									var directoryName = Path.GetDirectoryName(filePath);
									if (!string.IsNullOrEmpty(directoryName))
									{
										var projectName = Path.GetFileNameWithoutExtension(projectInfoNode.Attributes["ProjectFilePath"]?.Value);
										filePath = Path.Combine(directoryName, $"{projectName}.ProjectFiles", "Reports");
									}
								}
							}
						}
					}

					ReportsFolderPath = filePath;
					return ReportFileExist(filePath);
				}
			}
			catch (Exception ex)
			{
				Log.Logger.Error($"ReportFolderExist method: {ex.Message}\n {ex.StackTrace}");
			}

			return false;
		}

		/// <summary>
		///  Save report output path within json file
		/// (It is saved within a json file, because it is a general path and is not related to an individual project)
		/// </summary>
		/// <param name="reportOutputPath"></param>
		public void SaveExportPath(string reportOutputPath)
		{
			if (!string.IsNullOrEmpty(reportOutputPath))
			{
				Directory.CreateDirectory(_communityFolderPath);

				var jsonExportPath = new JsonSettings { ExportPath = reportOutputPath };
				var jsonResult = JsonConvert.SerializeObject(jsonExportPath);

				if (File.Exists(JsonPath))
				{
					File.Delete(JsonPath);
				}

				File.Create(JsonPath).Dispose();

				using (var tw = new StreamWriter(JsonPath, true))
				{
					tw.WriteLine(jsonResult);
					tw.Close();
				}
			}
		}

		/// <summary>
		/// Check if the report was successfully generated
		/// </summary>
		/// <param name="reportOutputPath"></param>
		/// <param name="isChecked"></param>
		/// <param name="optionalInformation"></param>
		/// <param name="projects"></param>
		/// <returns></returns>
		public bool GenerateReportFile(BindingList<ProjectDetails> projects, OptionalInformation optionalInformation, string reportOutputPath, bool isChecked)
		{
			try
			{
				_help.CreateDirectory(reportOutputPath);
				var projectsToBeExported = projects.Where(p => p.ShouldBeExported).ToList();
				var areCheckedLanguages = projectsToBeExported.Any(p => p.LanguagesForPoject.Any(l => l.Value));
				if (!areCheckedLanguages && projectsToBeExported.Count >= 1)
				{
					_messageBoxService.ShowInformationMessage(PluginResources.SelectLanguage_Export_Message, PluginResources.ExportResult_Label);
					return false;
				}
				foreach (var project in projectsToBeExported)
				{
					// check which languages to export
					if (project.LanguagesForPoject != null)
					{
						var checkedLanguages = project.LanguagesForPoject.Where(l => l.Value).ToList();

						foreach (var languageReport in checkedLanguages)
						{
							if (string.IsNullOrEmpty(project.ReportPath))
							{
								project.ReportPath = reportOutputPath;
							}

							WriteReportFile(project, optionalInformation, languageReport, isChecked);
						}
					}
				}
				return true;
			}
			catch (Exception exception)
			{
				Log.Logger.Error($"GenerateReport method: {exception.Message}\n {exception.StackTrace}");
				throw;
			}
		}

		private bool ReportsFolderExists(string reportsFolderPath)
		{
			return !string.IsNullOrEmpty(reportsFolderPath) && ReportFileExist(reportsFolderPath);
		}

		/// <summary>
		/// Write the report file based on the Analyse file 
		/// </summary>
		/// <param name="project"></param>
		/// <param name="languageReport"></param>
		/// <param name="isChecked"></param>
		/// <param name="optionalInformation"></param>
		private void WriteReportFile(ProjectDetails project, OptionalInformation optionalInformation, KeyValuePair<string,bool> languageReport, bool isChecked)
		{
			var streamPath = Path.Combine($"{project.ReportPath}{Path.DirectorySeparatorChar}", $"{project.ProjectName}_{languageReport.Key}.csv");
			using (var sw = new StreamWriter(streamPath))
			{
				if (project.LanguageAnalysisReportPaths != null)
				{
					var analyseReportPath = project.LanguageAnalysisReportPaths.FirstOrDefault(l => l.Key.Equals(languageReport.Key));
					if (!analyseReportPath.Equals(new KeyValuePair<string, string>()))
					{
						var report = new StudioAnalysisReport(analyseReportPath.Value);
						sw.Write(report.ToCsv(isChecked, optionalInformation));
					}
				}
			}
		}
	}
}
