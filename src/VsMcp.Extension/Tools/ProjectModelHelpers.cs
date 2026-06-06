using System;
using System.Collections.Generic;
using EnvDTE;

namespace VsMcp.Extension.Tools
{
    internal static class ProjectModelHelpers
    {
        public const string SolutionFolderKind = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";
        public const string CppProjectKind = "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";

        public static List<Project> EnumerateSolutionProjects(Projects projectCollection)
        {
            var projects = new List<Project>();
            CollectProjects(projectCollection, projects);
            return projects;
        }

        public static string[] GetSolutionLanguages(Projects projectCollection)
        {
            var languages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var project in EnumerateSolutionProjects(projectCollection))
            {
                var language = GetProjectLanguage(project);
                if (!string.IsNullOrEmpty(language))
                    languages.Add(language);
            }
            var result = new string[languages.Count];
            languages.CopyTo(result);
            return result;
        }

        public static bool IsCppProject(Project project)
        {
            try
            {
                if (project.Kind == CppProjectKind) return true;
            }
            catch { }

            try
            {
                return project.CodeModel?.Language == CodeModelLanguageConstants.vsCMLanguageVC;
            }
            catch
            {
                return false;
            }
        }

        public static string GetProjectLanguage(Project project)
        {
            try
            {
                if (project.Kind == CppProjectKind)
                    return "C++";

                if (project.Kind == VSLangProj.PrjKind.prjKindCSharpProject)
                    return "C#";

                if (project.Kind == VSLangProj.PrjKind.prjKindVBProject)
                    return "Visual Basic";
            }
            catch { }

            try
            {
                var language = project.CodeModel?.Language;
                if (language == CodeModelLanguageConstants.vsCMLanguageVC)
                    return "C++";

                if (language == CodeModelLanguageConstants.vsCMLanguageCSharp)
                    return "C#";

                if (language == CodeModelLanguageConstants.vsCMLanguageVB)
                    return "Visual Basic";

                return language;
            }
            catch { }

            try
            {
                var extension = System.IO.Path.GetExtension(project.FileName);
                if (string.Equals(extension, ".vcxproj", StringComparison.OrdinalIgnoreCase))
                    return "C++";

                if (string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase))
                    return "C#";

                if (string.Equals(extension, ".vbproj", StringComparison.OrdinalIgnoreCase))
                    return "Visual Basic";
            }
            catch { }

            return "";
        }

        private static void CollectProjects(Projects projectCollection, List<Project> result)
        {
            foreach (Project project in projectCollection)
            {
                try { CollectProject(project, result); }
                catch { }
            }
        }

        private static void CollectProject(Project project, List<Project> result)
        {
            if (project == null) return;

            if (project.Kind == SolutionFolderKind)
            {
                if (project.ProjectItems == null) return;

                foreach (ProjectItem item in project.ProjectItems)
                {
                    try
                    {
                        if (item.SubProject != null)
                            CollectProject(item.SubProject, result);
                    }
                    catch { }
                }
                return;
            }

            result.Add(project);
        }
    }
}
