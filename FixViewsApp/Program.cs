using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FixViewsApp
{
    class Program
    {
        static void Main(string[] args)
        {
            string rootDir = @"..\";
            string[] workspaceFiles = Directory.GetFiles(rootDir, "*WorkspaceView.xaml", SearchOption.AllDirectories);
            var monitorLayoutFiles = Directory.GetFiles(rootDir, "MonitorLayoutView.xaml", SearchOption.AllDirectories);
            
            var allFiles = new System.Collections.Generic.List<string>(workspaceFiles);
            allFiles.AddRange(monitorLayoutFiles);

            foreach (var file in allFiles)
            {
                string content = File.ReadAllText(file);
                
                // Align Grid.Column="0"
                content = Regex.Replace(content, @"<Grid Grid\.Column=""0""[^>]*Margin=""[^""]*""[^>]*>", m => {
                    return Regex.Replace(m.Value, @"Margin=""[^""]*""", @"Margin=""10""");
                });

                // Align Grid.Column="1"
                content = Regex.Replace(content, @"<Grid Grid\.Column=""1""[^>]*Margin=""[^""]*""[^>]*>", m => {
                    return Regex.Replace(m.Value, @"Margin=""[^""]*""", @"Margin=""0,10,10,10""");
                });

                // Align Grid.Column="2"
                content = Regex.Replace(content, @"<Grid Grid\.Column=""2""[^>]*Margin=""[^""]*""[^>]*>", m => {
                    return Regex.Replace(m.Value, @"Margin=""[^""]*""", @"Margin=""0,10,10,10""");
                });

                // Fix internal row margins (Grid.Row="0" -> Margin="0,0,0,10", Grid.Row="1" -> Margin="0")
                content = Regex.Replace(content, @"<Grid Grid\.Row=""0""[^>]*Margin=""[^""]*""[^>]*>", m => {
                    return Regex.Replace(m.Value, @"Margin=""[^""]*""", @"Margin=""0,0,0,10""");
                });

                content = Regex.Replace(content, @"<Grid Grid\.Row=""1""[^>]*Margin=""[^""]*""[^>]*>", m => {
                    return Regex.Replace(m.Value, @"Margin=""[^""]*""", @"Margin=""0""");
                });

                // Write changes
                File.WriteAllText(file, content);
                Console.WriteLine("Fixed margins in: " + Path.GetFileName(file));
                Console.WriteLine("Fixed margins in: " + Path.GetFileName(file));
            }
        }
    }
}
