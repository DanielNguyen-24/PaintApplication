using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using PaintApplication.Models;
using System.IO;

namespace PaintApplication.Services
{
    class FileService
    {
        public ProjectModel? OpenProject()
        {
            var dlg = new OpenFileDialog { Filter = "Paint Project (*.json)|*.json" };
            if (dlg.ShowDialog() == true)
            {
                var json = System.IO.File.ReadAllText(dlg.FileName);
                return System.Text.Json.JsonSerializer.Deserialize<ProjectModel>(json);
            }
            return null;
        }

        public void SaveProject(ProjectModel project)
        {
            var dlg = new SaveFileDialog { Filter = "Paint Project (*.json)|*.json" };
            if (dlg.ShowDialog() == true)
            {
                var json = JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json);
            }
        }
    }
}
