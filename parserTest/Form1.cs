using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace parserTest
{
    public partial class Form1 : Form
    {
        private const string rootName = "<root>";
        private List<JsonPathParser.ParsedProperty> _pathList;

        public Form1()
        {
            InitializeComponent();
        }

        private void ListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem == null)
                return;

            //var item = pathList.Where(n => n.Path == listBox1.SelectedItem.ToString());
            var item = _pathList[listBox1.SelectedIndex];

            var startPos = item.StartPosition;
            var endPos = item.EndPosition;

            if (startPos < 0 || startPos >= textBox.TextLength)
                return;
            if (endPos < 0 || endPos >= textBox.TextLength)
                return;

            textBox.SelectionStart = startPos;
            textBox.SelectionLength = endPos - startPos + 1;
            textBox.ScrollToCaret();
            listBox1.Focus();
        }

        private void Button_open_Click(object sender, EventArgs e)
        {
            openFileDialog.FileName = "";
            openFileDialog.Title = "Open json file";
            openFileDialog.DefaultExt = "jsonc";
            openFileDialog.Filter = "Kinetic files|*.jsonc|JSON files|*.json|All files|*.*";
            openFileDialog.ShowDialog();
        }

        private void OpenFileDialog_FileOk(object sender, CancelEventArgs e)
        {
            listBox1.Items.Clear();
            treeView1.Nodes.Clear();
            //textBox.Text = JsonIO.BeautifyJson(File.ReadAllText(openFileDialog.FileName), true);
            textBox.Text = File.ReadAllText(openFileDialog.FileName).Replace(' ', ' ');
            Text = openFileDialog.FileName;
            _pathList = new List<JsonPathParser.ParsedProperty>();
            _pathList = JsonPathParser.ParseJsonToPathList(textBox.Text).ToList();
            listBox1.Items.AddRange(_pathList.Select(n => n.Path + " (" + n.Type + ")").ToArray());
            
            var rootNode = JsonPathParser.ConvertPathListToTree(_pathList);
            //if (rootNode!=null) rootNode.Nodes[0].Text = rootName;

            treeView1.Nodes.Add(rootNode);
        }

        private void Button_dir_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK &&
                !string.IsNullOrEmpty(folderBrowserDialog1.SelectedPath))
            {
                var filesList = Directory.GetFiles(folderBrowserDialog1.SelectedPath,
                    "*.jsonc",
                    SearchOption.AllDirectories);

                foreach (var file in filesList)
                {
                    var text = File.ReadAllText(file).Replace(' ', ' '); // replace &nbsp char with space
                    var pathIndex = JsonPathParser.ParseJsonToPathList(text, out var pos).ToArray();

                    // parsing failed
                    if (pos >= 0)
                    {
                        File.AppendAllText("_bad_file.txt",
                              file + Environment.NewLine + pos + Environment.NewLine + Environment.NewLine);
                    }
                    // not failed but have incorrect positions
                    var item1 = pathIndex.Where(n => n.EndPosition < 0 || n.StartPosition < 0).ToArray();
                    var jsonProperties = item1;
                    if (jsonProperties.Any())
                    {
                        File.AppendAllText("_bad_file.txt", file + Environment.NewLine);
                        foreach (var item in jsonProperties)
                        {
                            File.AppendAllText("_bad_file.txt",
                                item.Path + " = " + item.Value + Environment.NewLine);
                        }
                        File.AppendAllText("_bad_file.txt", Environment.NewLine);
                    }

                    // find duplicate json paths
                    var item2 = pathIndex.Where(n =>
                    n.Type != JsonPathParser.PropertyType.Comment)
                        .ToArray()
                        .GroupBy(n => n.Path)
                        .Where(n => n.Count() > 1).ToArray();

                    var enumerates = item2;
                    if (enumerates.Any())
                    {
                        File.AppendAllText("_dup_file.txt", file + Environment.NewLine);
                        foreach (var item3 in enumerates)
                        {
                            foreach (var item4 in item3)
                            {
                                File.AppendAllText("_dup_file.txt", item4.Path
                                  + " = " + item4.Value + Environment.NewLine);
                            }
                        }
                        File.AppendAllText("_dup_file.txt", Environment.NewLine);
                    }
                }
            }
        }

        private void TreeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var path = e.Node.FullPath;
            if (path.StartsWith("\\")) path=path.Substring(1);
            var item = _pathList.FirstOrDefault(n => n.Path == path.Replace('\\', '.').Replace(rootName, ""));
            if (item == null) return;

            var startPos = item.StartPosition;
            var endPos = item.EndPosition;

            if (startPos < 0 || startPos >= textBox.TextLength)
                return;
            if (endPos < 0 || endPos >= textBox.TextLength)
                return;

            textBox.SelectionStart = startPos;
            textBox.SelectionLength = endPos - startPos + 1;
            textBox.ScrollToCaret();
            listBox1.Focus();
        }
    }
}