﻿
// Copyright (c) AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OmniSharp.Solution
{
    public interface ISolution
    {
        List<IProject> Projects { get; }
        string FileName { get; }
        CSharpFile GetFile(string filename);
        IProject ProjectContainingFile(string filename);
        void Reload();
    }

    public class CSharpSolution : ISolution
    {
        public string Directory;
        public List<IProject> Projects { get; private set; }

        private OrphanProject _orphanProject;

        public string FileName { get; private set; }

        public CSharpSolution(string fileName)
        {
            new Action<string>(LoadSolution).BeginInvoke(fileName, null, null);
        }

        private void LoadSolution(string fileName)
        {
            FileName = fileName;
            _orphanProject = new OrphanProject(this);
            Projects = new List<IProject>();
            Directory = Path.GetDirectoryName(fileName);
            var projectLinePattern =
                new Regex(
                    "Project\\(\"(?<TypeGuid>.*)\"\\)\\s+=\\s+\"(?<Title>.*)\",\\s*\"(?<Location>.*)\",\\s*\"(?<Guid>.*)\"");

            foreach (string line in File.ReadLines(fileName))
            {
                Match match = projectLinePattern.Match(line);
                if (match.Success)
                {
                    string typeGuid = match.Groups["TypeGuid"].Value;
                    string title = match.Groups["Title"].Value;
                    string location = Path.Combine(Directory, match.Groups["Location"].Value).FixPath();
                    string guid = match.Groups["Guid"].Value;
                    switch (typeGuid.ToUpperInvariant())
                    {
                        case "{2150E333-8FDC-42A3-9474-1A3956D46DE8}": // Solution Folder
                            // ignore folders
                            break;
                        case "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}": // C# project
                        case "{CFBF81BE-A5A8-8323-0E2F-C9B25881C624}":
                        case "{4C2D6F16-28F7-6964-F259-E4608E3E113F}":
                            Console.WriteLine("Loading project - " + title);
                            Projects.Add(new CSharpProject(this, title, location));
                            break;
                        default:
                            Console.WriteLine("Project {0} has unsupported type {1}", location, typeGuid);
                            break;
                    }
                }
            }
        }

        public CSharpFile GetFile(string filename)
        {
            return (from project in Projects
                    from file in project.Files
                    where file.FileName.Equals(filename, StringComparison.InvariantCultureIgnoreCase)
                    select file).FirstOrDefault();
        }

        public IProject ProjectContainingFile(string filename)
        {
            var project = Projects.FirstOrDefault(p => p.Files.Any(f => f.FileName.Equals(filename, StringComparison.InvariantCultureIgnoreCase)));
            if (project == null)
            {
                var file = new FileInfo(filename);
                var directory = file.Directory;
                var projectFiles = directory.GetFiles("*.csproj");
                while (!projectFiles.Any() && directory.Parent != null)
                {
                    directory = directory.Parent;
                    projectFiles = directory.GetFiles("*.csproj");
                }

                if (projectFiles.Any())
                {
                    if (File.Exists(filename))
                    {
                        var projectFile = projectFiles.First();
                        project = Projects.First(p => projectFile.FullName.Contains(p.FileName));
                        project.Files.Add(new CSharpFile(project, filename));
                    }
                }
            }
            return project ?? _orphanProject;
        }

        public void Reload()
        {
            LoadSolution(this.FileName);
        }
    }
}


