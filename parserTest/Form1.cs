using JsonPathParserLib;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace parserTest
{
    public partial class Form1 : Form
    {
        private const string RootName = "<root>";
        private char _pathDivider = '.';
        private JsonPathParser _parser;
        private const string LogFileName = "_bad_file.txt";

        public Form1()
        {
            InitializeComponent();
        }

        private void Button_open_Click(object sender, EventArgs e)
        {
            openFileDialog.FileName = "";
            openFileDialog.Title = "Open json file";
            openFileDialog.DefaultExt = "json";
            openFileDialog.Filter = "JSON files|*.json|All files|*.*";
            openFileDialog.ShowDialog();
        }

        private void OpenFileDialog_FileOk(object sender, CancelEventArgs e)
        {
            listBox1.DataSource = null;
            listBox1.Items.Clear();
            treeView1.Nodes.Clear();
            textBox_errorMessage.Clear();

            var jsonText = File.ReadAllText(openFileDialog.FileName);
            textBox.Text = jsonText;
            Text = openFileDialog.FileName;
            _pathDivider = treeView1.PathSeparator.FirstOrDefault();
            _parser = new JsonPathParser
            {
                JsonPathDivider = _pathDivider,
                RootName = RootName,
                TrimComplexValues = false,
                SaveComplexValues = true,
                KeepComments = true,
            };

            var pos = -1;
            var errorFound = string.Empty;
            var pathList = new List<ParsedProperty>();
            try
            {
                pathList = _parser.ParseJsonToPathList(jsonText);
            }
            catch (ParseException ex)
            {
                pos = ex.Position;
                errorFound = $"Exception parsing file: {openFileDialog.FileName} at position {pos}:  {ex.Message}";
                pathList = ex.PathList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception: {ex}");
            }

            if (!string.IsNullOrEmpty(errorFound))
            {
                textBox_errorMessage.Text = errorFound;
                MessageBox.Show(errorFound);
                textBox.SelectionStart = pos;
                textBox.SelectionLength = textBox.Text.Length - pos + 1;
                textBox.ScrollToCaret();
                listBox1.Focus();
            }

            listBox1.DataSource = pathList;
            var rootNodes = ConvertPathListToTree(pathList);
            if (rootNodes != null)
                treeView1.Nodes.AddRange(rootNodes);
        }

        private void Button_dir_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() != DialogResult.OK ||
                string.IsNullOrEmpty(folderBrowserDialog1.SelectedPath))
                return;

            var filesList = Directory.GetFiles(folderBrowserDialog1.SelectedPath,
                "*.json*",
                SearchOption.AllDirectories);

            var errors = new List<(string file, string message)>();
            foreach (var file in filesList)
            {
                string text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch (Exception ex)
                {
                    errors.Add((file, $"Error reading file: {ex.Message}"));
                    continue;
                }

                // replace &nbsp char with space
                if (text.Contains((char)160))
                {
                    text = text.Replace((char)160, (char)32);
                    errors.Add((file, "&nbsp char in the file"));
                }

                var parser = new JsonPathParser
                {
                    JsonPathDivider = _pathDivider,
                    RootName = RootName,
                    TrimComplexValues = false,
                    SaveComplexValues = true,
                    KeepComments = true,
                };

                var errorFound = string.Empty;
                var pathList = Array.Empty<ParsedProperty>();
                try
                {
                    pathList = parser.ParseJsonToPathList(text).ToArray();
                }
                catch (ParseException ex)
                {
                    var pos = ex.Position;
                    errorFound = $"Exception parsing file: {file} at position {pos}:  {ex.Message}";
                    errors.Add((file, errorFound));
                }
                catch (Exception ex)
                {
                    errorFound = $"Exception parsing file: {file}:  {ex.Message}";
                    errors.Add((file, errorFound));
                }

                // parsing failed
                if (!string.IsNullOrEmpty(errorFound))
                    errors.Add((file, errorFound));

                if (pathList.Length <= 0)
                    continue;

                // not failed but have incorrect positions
                var incorrectPositions = pathList.Where(n => n.EndPosition < 0 || n.StartPosition < 0).ToArray();
                if (incorrectPositions.Any())
                    errors.AddRange(incorrectPositions.Select(item => (file,
                        $"Incorrect [{item.Path}] object positions: start [{item.StartPosition}], end [{item.EndPosition}]")));

                // find duplicate json paths
                var duplicatePaths = pathList
                    .Where(n => n.JsonPropertyType != JsonPropertyType.Comment)
                    .GroupBy(n => n.Path)
                    .Where(n => n.Count() > 1)
                    .ToArray();

                if (!duplicatePaths.Any())
                    continue;

                errors.AddRange(duplicatePaths.SelectMany(path => path,
                    (path, dupItem) => (file, $"Duplicate path {dupItem.Path} at position {dupItem.StartPosition}")));
            }

            var errorMessages = errors.Aggregate(string.Empty,
                (current, item) => current + $"{item.file}: {item.message}{Environment.NewLine}");

            if (!string.IsNullOrEmpty(errorMessages))
            {
                File.AppendAllText(LogFileName, errorMessages);
                MessageBox.Show($"Errors found: {errors.Count}\r\nSee log in the \"{LogFileName}\"");
            }
        }

        private void TreeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (!(e.Node.Tag is ParsedProperty item))
            {
                ClearFields();
                return;
            }

            FillFields(item);
            listBox1.Focus();
        }

        private void ListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!(listBox1.SelectedItem is ParsedProperty item))
            {
                ClearFields();
                return;
            }

            FillFields(item);
            listBox1.Focus();
        }

        private void ClearFields()
        {
            textBox_name.Text = "";
            textBox_value.Text = "";
            textBox_path.Text = "";
            textBox_depth.Text = "";
            textBox_propertyType.Text = "";
            textBox_valueType.Text = "";
            textBox_startPos.Text = "";
            textBox_endPos.Text = "";
            textBox_startLine.Text = "";
            textBox_endLine.Text = "";
            textBox.SelectionLength = 0;
        }

        private void FillFields(ParsedProperty item)
        {
            var startPos = item.StartPosition;
            var endPos = item.EndPosition;

            textBox_name.Text = item.Name;
            textBox_value.Text = item.Value;
            textBox_path.Text = item.Path;
            textBox_depth.Text = item.Depth.ToString();
            textBox_propertyType.Text = item.JsonPropertyType.ToString();
            textBox_valueType.Text = item.ValueType.ToString();
            textBox_startPos.Text = item.StartPosition.ToString();
            textBox_endPos.Text = item.EndPosition.ToString();

            JsonPathParser.GetLinesNumber(textBox.Text, item.StartPosition, item.EndPosition, out var startLine,
                out var endLine);
            textBox_startLine.Text = startLine.ToString();
            textBox_endLine.Text = endLine.ToString();

            textBox.SelectionLength = 0;
            if (startPos < 0 || startPos >= textBox.TextLength)
                return;

            if (endPos < 0 || endPos >= textBox.TextLength)
                return;

            textBox.SelectionStart = startPos;
            textBox.SelectionLength = endPos - startPos + 1;
            textBox.ScrollToCaret();
        }

        private TreeNode[] ConvertPathListToTree(IEnumerable<ParsedProperty> pathList)
        {
            if (pathList == null)
                return Array.Empty<TreeNode>();

            pathList = _parser.ConvertForTreeProcessing(pathList);
            var node = new TreeNode("?");
            foreach (var propertyItem in pathList)
            {
                var itemPath = propertyItem.Path;
                var tmpNode = node?.LastNode;
                var tmpPath = new StringBuilder();
                foreach (var token in itemPath.Split(_pathDivider))
                {
                    var nodeName = token;
                    tmpPath.Append(token + _pathDivider);
                    if (propertyItem.JsonPropertyType == JsonPropertyType.Array)
                        nodeName += "[]";
                    else if (propertyItem.JsonPropertyType == JsonPropertyType.Object)
                        nodeName += "{}";

                    if (tmpNode == null)
                    {
                        var newNode = new TreeNode(nodeName)
                        {
                            Tag = propertyItem,
                            Name = tmpPath.ToString()
                        };
                        node.Nodes.Add(newNode);
                        continue;
                    }

                    if (tmpPath.ToString() == RootName + _pathDivider && itemPath != RootName)
                        continue;

                    if (itemPath == RootName)
                    {
                        var newNode = new TreeNode(nodeName)
                        {
                            Tag = propertyItem,
                            Name = tmpPath.ToString()
                        };

                        node.Nodes.Add(newNode);
                    }
                    else if (!tmpNode.Nodes.ContainsKey(tmpPath.ToString()))
                    {
                        var newNode = new TreeNode(nodeName)
                        {
                            Tag = propertyItem,
                            Name = tmpPath.ToString()
                        };

                        tmpNode.Nodes.Add(newNode);
                    }
                    else
                    {
                        tmpNode = tmpNode.Nodes[tmpPath.ToString()];
                    }
                }
            }

            return node?.Nodes.OfType<TreeNode>().ToArray();
        }
    }
}