using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using JsonPathParserLib;

namespace parserTest
{
    public partial class Form1 : Form
    {
        private const string RootName = "<root>";
        private char PathDivider = '.';

        public Form1()
        {
            InitializeComponent();
            //listBox1.DisplayMember = "Path";
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
            var jsonText = File.ReadAllText(openFileDialog.FileName).Replace(' ', ' ');
            textBox.Text = jsonText;
            Text = openFileDialog.FileName;
            var pos = -1;
            var errorFound = false;
            PathDivider = treeView1.PathSeparator.FirstOrDefault();
            ParsedProperty[] pathList = null;

            var parcer = new JsonPathParser
            {
                TrimComplexValues = false,
                SaveComplexValues = true,
                RootName = RootName,
                JsonPathDivider = PathDivider
            };

            try
            {
                pathList = parcer.ParseJsonToPathList(jsonText, out pos, out errorFound).ToArray();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception: " + ex);
            }

            if (errorFound)
            {
                MessageBox.Show("Error parsing file at position: " + pos.ToString());
                textBox.SelectionStart = pos;
                textBox.SelectionLength = textBox.Text.Length - pos + 1;
                textBox.ScrollToCaret();
                listBox1.Focus();
            }

            listBox1.DataSource = pathList;

            var rootNodes = ConvertPathListToTree(pathList);
            treeView1.Nodes.AddRange(rootNodes);
        }

        private void Button_dir_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() != DialogResult.OK ||
                string.IsNullOrEmpty(folderBrowserDialog1.SelectedPath))
                return;

            var filesList = Directory.GetFiles(folderBrowserDialog1.SelectedPath,
                "*.jsonc",
                SearchOption.AllDirectories);

            foreach (var file in filesList)
            {
                var text = File.ReadAllText(file).Replace(' ', ' '); // replace &nbsp char with space

                var pos = -1;
                var errorFound = false;
                ParsedProperty[] pathList = null;

                var parcer = new JsonPathParser
                {
                    TrimComplexValues = false,
                    SaveComplexValues = true,
                    RootName = RootName,
                    JsonPathDivider = PathDivider
                };

                try
                {
                    pathList = parcer.ParseJsonToPathList(text, out pos, out errorFound).ToArray();
                }
                catch (Exception)
                {
                    File.AppendAllText("_bad_file.txt",
                        file + Environment.NewLine + pos + Environment.NewLine + Environment.NewLine);
                }

                // parsing failed
                if (errorFound)
                {
                    File.AppendAllText("_bad_file.txt",
                        file + Environment.NewLine + pos + Environment.NewLine + Environment.NewLine);
                }
                // not failed but have incorrect positions
                var item1 = pathList.Where(n => n.EndPosition < 0 || n.StartPosition < 0).ToArray();
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
                var item2 = pathList.Where(n =>
                        n.PropertyType != PropertyType.Comment)
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

        private void TreeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (!(e.Node.Tag is ParsedProperty item))
            {
                textBox_name.Text = "";
                textBox_value.Text = "";
                textBox_propertyType.Text = "";
                textBox_valueType.Text = "";
                textBox_startPos.Text = "";
                textBox_endPos.Text = "";
                textBox.SelectionLength = 0;
                return;
            }

            var startPos = item.StartPosition;
            var endPos = item.EndPosition;

            if (startPos < 0 || startPos >= textBox.TextLength)
                return;

            if (endPos < 0 || endPos >= textBox.TextLength)
                return;

            textBox_name.Text = item.Name;
            textBox_value.Text = item.Value;
            textBox_propertyType.Text = item.PropertyType.ToString();
            textBox_valueType.Text = item.ValueType.ToString();
            textBox_startPos.Text = item.StartPosition.ToString();
            textBox_endPos.Text = item.EndPosition.ToString();

            textBox.SelectionStart = startPos;
            textBox.SelectionLength = endPos - startPos + 1;
            textBox.ScrollToCaret();
            listBox1.Focus();
        }

        private void ListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!(listBox1.SelectedItem is ParsedProperty item))
            {
                textBox_name.Text = "";
                textBox_value.Text = "";
                textBox_propertyType.Text = "";
                textBox_valueType.Text = "";
                textBox_startPos.Text = "";
                textBox_endPos.Text = "";
                textBox.SelectionLength = 0;
                return;
            }

            var startPos = item.StartPosition;
            var endPos = item.EndPosition;

            textBox.SelectionLength = 0;
            if (startPos < 0 || startPos >= textBox.TextLength)
                return;
            if (endPos < 0 || endPos >= textBox.TextLength)
                return;

            textBox_name.Text = item.Name;
            textBox_value.Text = item.Value;
            textBox_propertyType.Text = item.PropertyType.ToString();
            textBox_valueType.Text = item.ValueType.ToString();
            textBox_startPos.Text = item.StartPosition.ToString();
            textBox_endPos.Text = item.EndPosition.ToString();

            textBox.SelectionStart = startPos;
            textBox.SelectionLength = endPos - startPos + 1;
            textBox.ScrollToCaret();
            listBox1.Focus();
        }

        public TreeNode[] ConvertPathListToTree(ParsedProperty[] pathList)
        {
            TreeNode node = null;

            foreach (var propertyItem in pathList)
            {
                var itemPath = propertyItem.Path;

                var tmpNode = node;
                var tmpPath = new StringBuilder();
                foreach (var token in itemPath.Split(new char[] { PathDivider }))
                {
                    tmpPath.Append(token + PathDivider.ToString());

                    if (tmpNode == null)
                    {
                        node = new TreeNode(token)
                        {
                            Tag = propertyItem,
                            Name = tmpPath.ToString()
                        };

                        tmpNode = node;
                    }

                    if (!tmpNode.Nodes.ContainsKey(tmpPath.ToString()))
                    {
                        var newNode = new TreeNode(token)
                        {
                            Tag = propertyItem,
                            Name = tmpPath.ToString()
                        };

                        tmpNode.Nodes.Add(newNode);
                    }

                    tmpNode = tmpNode.Nodes[tmpPath.ToString()];
                }
            }

            return node.Nodes.OfType<TreeNode>().ToArray();
        }
    }
}