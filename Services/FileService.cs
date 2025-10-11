using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using PaintApplication.Models;

namespace PaintApplication.Services
{
    public class FileService
    {
        public ProjectModel? OpenProject()
        {
            var dlg = new OpenFileDialog { Filter = "Paint Project (*.json)|*.json" };
            if (dlg.ShowDialog() == true)
            {
                var json = File.ReadAllText(dlg.FileName);
                return JsonSerializer.Deserialize<ProjectModel>(json);
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
