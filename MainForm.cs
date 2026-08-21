using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ImasKoreanPatcher
{
    public sealed class MainForm : Form
    {
        private static readonly bool ShowCommunicationPerfectCheat = false;

        private Panel dropPanel;
        private Label dropTitleLabel;
        private Label dropHintLabel;
        private Label isoStatusLabel;
        private Label xexToolStatusLabel;
        private Label titleUpdateStatusLabel;
        private CheckBox applyTitleUpdateCheckBox;
        private CheckBox doubleAuditionFansCheckBox;
        private CheckBox ensureAuditionPassCountCheckBox;
        private CheckBox doubleLessonGainsCheckBox;
        private CheckBox specialAudition3AlwaysOpenCheckBox;
        private CheckBox activityStopMenuCheckBox;
        private CheckBox communicationPerfectCheckBox;
        private Button patchButton;
        private ProgressBar progressBar;
        private TextBox statusTextBox;

        private string selectedIsoPath;
        private string selectedXexToolPath;
        private string selectedTitleUpdatePath;
        private BackgroundWorker patchWorker;

        private sealed class PatchProgressUpdate
        {
            public PatchProgressUpdate(string message, bool appendToLog)
            {
                Message = message;
                AppendToLog = appendToLog;
            }

            public string Message { get; private set; }
            public bool AppendToLog { get; private set; }
        }

        public MainForm()
        {
            Text = "아이돌마스터 한글 패치";
            Font = new Font("Malgun Gothic", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(246, 247, 250);
            MinimumSize = new Size(980, 620);
            Size = new Size(1080, 680);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;

            TableLayoutPanel root = new TableLayoutPanel();
            root.ColumnCount = 5;
            root.RowCount = 3;
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(18);
            root.BackColor = BackColor;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 124F));
            Controls.Add(root);

            dropPanel = BuildDropPanel();
            root.Controls.Add(dropPanel, 0, 0);

            root.Controls.Add(BuildVerticalSeparator(), 1, 0);

            Panel optionsPanel = BuildOptionsPanel();
            root.Controls.Add(optionsPanel, 2, 0);

            root.Controls.Add(BuildVerticalSeparator(), 3, 0);

            Panel buttonPanel = BuildPatchButtonPanel();
            root.Controls.Add(buttonPanel, 4, 0);

            progressBar = new ProgressBar();
            progressBar.Dock = DockStyle.Fill;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            progressBar.Style = ProgressBarStyle.Continuous;
            root.Controls.Add(progressBar, 0, 1);
            root.SetColumnSpan(progressBar, 5);

            statusTextBox = new TextBox();
            statusTextBox.Dock = DockStyle.Fill;
            statusTextBox.BorderStyle = BorderStyle.FixedSingle;
            statusTextBox.BackColor = Color.White;
            statusTextBox.ForeColor = Color.FromArgb(65, 72, 86);
            statusTextBox.ReadOnly = true;
            statusTextBox.Multiline = true;
            statusTextBox.ScrollBars = ScrollBars.Vertical;
            statusTextBox.WordWrap = false;
            statusTextBox.ShortcutsEnabled = true;
            statusTextBox.TabStop = true;
            statusTextBox.Text = "대기 중";
            root.Controls.Add(statusTextBox, 0, 2);
            root.SetColumnSpan(statusTextBox, 5);

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            UpdateFileStatusDisplay();
        }

        private Panel BuildDropPanel()
        {
            Panel panel = new Panel();
            panel.AllowDrop = true;
            panel.BackColor = Color.White;
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0, 0, 14, 14);
            panel.Cursor = Cursors.Hand;

            TableLayoutPanel inner = new TableLayoutPanel();
            inner.ColumnCount = 1;
            inner.RowCount = 7;
            inner.Dock = DockStyle.Fill;
            inner.Padding = new Padding(22);
            inner.BackColor = Color.White;
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            panel.Controls.Add(inner);

            dropTitleLabel = new Label();
            dropTitleLabel.Dock = DockStyle.Fill;
            dropTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            dropTitleLabel.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold, GraphicsUnit.Point);
            dropTitleLabel.ForeColor = Color.FromArgb(34, 42, 54);
            dropTitleLabel.AutoSize = false;
            dropTitleLabel.UseMnemonic = false;
            dropTitleLabel.Text = "ISO 및 관련 파일을 여기에 드래그 & 드롭";
            inner.Controls.Add(dropTitleLabel, 0, 0);

            dropHintLabel = new Label();
            dropHintLabel.Dock = DockStyle.Fill;
            dropHintLabel.TextAlign = ContentAlignment.TopLeft;
            dropHintLabel.AutoEllipsis = true;
            dropHintLabel.UseMnemonic = false;
            dropHintLabel.ForeColor = Color.FromArgb(92, 101, 116);
            dropHintLabel.Text = "또는 클릭해서 파일 선택";
            inner.Controls.Add(dropHintLabel, 0, 1);

            isoStatusLabel = CreateFileStatusLabel();
            inner.Controls.Add(isoStatusLabel, 0, 3);

            xexToolStatusLabel = CreateFileStatusLabel();
            inner.Controls.Add(xexToolStatusLabel, 0, 4);

            titleUpdateStatusLabel = CreateFileStatusLabel();
            inner.Controls.Add(titleUpdateStatusLabel, 0, 5);

            AttachDropPanelHandlers(panel);

            return panel;
        }

        private Label CreateFileStatusLabel()
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.AutoEllipsis = true;
            label.UseMnemonic = false;
            label.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold, GraphicsUnit.Point);
            label.Margin = new Padding(0);
            return label;
        }

        private void AttachDropPanelHandlers(Control control)
        {
            control.AllowDrop = true;
            control.Click += OnDropPanelClick;
            control.DragEnter += OnDragEnter;
            control.DragDrop += OnDragDrop;
            foreach (Control child in control.Controls)
            {
                AttachDropPanelHandlers(child);
            }
        }

        private Panel BuildVerticalSeparator()
        {
            Panel separator = new Panel();
            separator.BackColor = Color.FromArgb(205, 211, 222);
            separator.Dock = DockStyle.Fill;
            separator.Margin = new Padding(0, 8, 0, 22);
            return separator;
        }

        private Panel BuildOptionsPanel()
        {
            Panel panel = new Panel();
            panel.BackColor = BackColor;
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(16, 0, 12, 14);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.ColumnCount = 1;
            layout.RowCount = 13;
            layout.Dock = DockStyle.Fill;
            layout.BackColor = BackColor;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ShowCommunicationPerfectCheat ? 34F : 0F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            panel.Controls.Add(layout);

            Label patchOptionsLabel = new Label();
            patchOptionsLabel.Dock = DockStyle.Fill;
            patchOptionsLabel.Text = "패치 옵션";
            patchOptionsLabel.TextAlign = ContentAlignment.MiddleLeft;
            patchOptionsLabel.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold, GraphicsUnit.Point);
            patchOptionsLabel.ForeColor = Color.FromArgb(34, 42, 54);
            patchOptionsLabel.Margin = new Padding(0);
            layout.Controls.Add(patchOptionsLabel, 0, 1);

            applyTitleUpdateCheckBox = new CheckBox();
            applyTitleUpdateCheckBox.Anchor = AnchorStyles.Left;
            applyTitleUpdateCheckBox.AutoSize = true;
            applyTitleUpdateCheckBox.Text = "타이틀 업데이트 반영";
            applyTitleUpdateCheckBox.ForeColor = Color.FromArgb(65, 72, 86);
            applyTitleUpdateCheckBox.Margin = new Padding(0);
            applyTitleUpdateCheckBox.Enabled = false;
            layout.Controls.Add(applyTitleUpdateCheckBox, 0, 2);

            Label titleUpdateDescription = CreateOptionDescription(
                "- 타이틀 업데이트가 반영된 ISO를 생성합니다.");
            layout.Controls.Add(titleUpdateDescription, 0, 3);

            Label cheatLabel = new Label();
            cheatLabel.Dock = DockStyle.Fill;
            cheatLabel.Text = "치트";
            cheatLabel.TextAlign = ContentAlignment.MiddleLeft;
            cheatLabel.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold, GraphicsUnit.Point);
            cheatLabel.ForeColor = Color.FromArgb(34, 42, 54);
            cheatLabel.Margin = new Padding(0);
            layout.Controls.Add(cheatLabel, 0, 5);

            doubleAuditionFansCheckBox = new CheckBox();
            doubleAuditionFansCheckBox.Anchor = AnchorStyles.Left;
            doubleAuditionFansCheckBox.AutoSize = true;
            doubleAuditionFansCheckBox.Text = "오디션의 팬 증가량 2배";
            doubleAuditionFansCheckBox.ForeColor = Color.FromArgb(65, 72, 86);
            doubleAuditionFansCheckBox.Margin = new Padding(0);
            layout.Controls.Add(doubleAuditionFansCheckBox, 0, 6);

            ensureAuditionPassCountCheckBox = new CheckBox();
            ensureAuditionPassCountCheckBox.Anchor = AnchorStyles.Left;
            ensureAuditionPassCountCheckBox.AutoSize = true;
            ensureAuditionPassCountCheckBox.Text = "오디션의 합격자수 2명이상으로 변경";
            ensureAuditionPassCountCheckBox.ForeColor = Color.FromArgb(65, 72, 86);
            ensureAuditionPassCountCheckBox.Margin = new Padding(0);
            layout.Controls.Add(ensureAuditionPassCountCheckBox, 0, 7);

            doubleLessonGainsCheckBox = new CheckBox();
            doubleLessonGainsCheckBox.Anchor = AnchorStyles.Left;
            doubleLessonGainsCheckBox.AutoSize = true;
            doubleLessonGainsCheckBox.Text = "레슨의 능력치 상승 2배";
            doubleLessonGainsCheckBox.ForeColor = Color.FromArgb(65, 72, 86);
            doubleLessonGainsCheckBox.Margin = new Padding(0);
            layout.Controls.Add(doubleLessonGainsCheckBox, 0, 8);

            specialAudition3AlwaysOpenCheckBox = new CheckBox();
            specialAudition3AlwaysOpenCheckBox.Anchor = AnchorStyles.Left;
            specialAudition3AlwaysOpenCheckBox.AutoSize = true;
            specialAudition3AlwaysOpenCheckBox.Text = "특별 오디션 3 상시 개방";
            specialAudition3AlwaysOpenCheckBox.ForeColor = Color.FromArgb(65, 72, 86);
            specialAudition3AlwaysOpenCheckBox.Margin = new Padding(0);
            specialAudition3AlwaysOpenCheckBox.Enabled = false;
            layout.Controls.Add(specialAudition3AlwaysOpenCheckBox, 0, 9);

            activityStopMenuCheckBox = new CheckBox();
            activityStopMenuCheckBox.Anchor = AnchorStyles.Left;
            activityStopMenuCheckBox.AutoSize = true;
            activityStopMenuCheckBox.Text = "활동 중단 메뉴 추가";
            activityStopMenuCheckBox.ForeColor = Color.FromArgb(65, 72, 86);
            activityStopMenuCheckBox.Margin = new Padding(0);
            activityStopMenuCheckBox.Enabled = false;
            layout.Controls.Add(activityStopMenuCheckBox, 0, 10);

            communicationPerfectCheckBox = new CheckBox();
            communicationPerfectCheckBox.Anchor = AnchorStyles.Left;
            communicationPerfectCheckBox.AutoSize = true;
            communicationPerfectCheckBox.Text = "영업 선택지 항상 퍼펙트";
            communicationPerfectCheckBox.ForeColor = Color.FromArgb(65, 72, 86);
            communicationPerfectCheckBox.Margin = new Padding(0);
            communicationPerfectCheckBox.Visible = ShowCommunicationPerfectCheat;
            communicationPerfectCheckBox.TabStop = ShowCommunicationPerfectCheat;
            layout.Controls.Add(communicationPerfectCheckBox, 0, 11);

            return panel;
        }

        private Label CreateOptionDescription(string text)
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.Text = text;
            label.TextAlign = ContentAlignment.TopLeft;
            label.Font = new Font(Font.FontFamily, 8F, FontStyle.Regular, GraphicsUnit.Point);
            label.ForeColor = Color.FromArgb(104, 112, 126);
            label.Padding = new Padding(22, 0, 0, 0);
            label.Margin = new Padding(0);
            label.AutoEllipsis = true;
            label.UseMnemonic = false;
            return label;
        }

        private Panel BuildPatchButtonPanel()
        {
            Panel panel = new Panel();
            panel.BackColor = BackColor;
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(8, 0, 0, 14);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.Dock = DockStyle.Fill;
            layout.BackColor = BackColor;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            panel.Controls.Add(layout);

            patchButton = new Button();
            patchButton.Anchor = AnchorStyles.None;
            patchButton.Size = new Size(166, 58);
            patchButton.Text = "한글 패치";
            patchButton.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold, GraphicsUnit.Point);
            patchButton.Enabled = false;
            patchButton.Click += OnPatchButtonClick;
            layout.Controls.Add(patchButton, 0, 1);

            return panel;
        }

        private void OnDropPanelClick(object sender, EventArgs e)
        {
            if (patchWorker != null && patchWorker.IsBusy)
            {
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "ISO 및 관련 파일 선택";
                dialog.Filter = "지원 파일 (*.iso; xextool.exe; TU*)|*.iso;xextool.exe;TU*;*.xexp;*.tu|ISO files (*.iso)|*.iso|xextool.exe|xextool.exe|Title Update files (TU*;*.xexp;*.tu)|TU*;*.xexp;*.tu|All files (*.*)|*.*";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    SelectRelatedFiles(dialog.FileNames);
                }
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            string[] paths = TryGetDroppedFiles(e);
            e.Effect = HasSupportedFile(paths) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] paths = TryGetDroppedFiles(e);
            if (!HasSupportedFile(paths))
            {
                SetStatus("지원하는 ISO, xextool.exe 또는 TU 파일을 선택해 주세요.");
                return;
            }

            SelectRelatedFiles(paths);
        }

        private static string[] TryGetDroppedFiles(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return null;
            }

            return e.Data.GetData(DataFormats.FileDrop) as string[];
        }

        private static bool HasSupportedFile(string[] paths)
        {
            if (paths == null)
            {
                return false;
            }

            for (int index = 0; index < paths.Length; index++)
            {
                if (IsIsoPath(paths[index]) || IsXexToolPath(paths[index]) || IsTitleUpdatePath(paths[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsIsoPath(string path)
        {
            return !String.IsNullOrEmpty(path)
                && File.Exists(path)
                && String.Equals(Path.GetExtension(path), ".iso", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsXexToolPath(string path)
        {
            return !String.IsNullOrEmpty(path)
                && File.Exists(path)
                && String.Equals(Path.GetFileName(path), "xextool.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatUnsupportedXexToolMessage(string detectedVersion)
        {
            if (String.IsNullOrEmpty(detectedVersion))
            {
                return "xextool.exe 6.3을 확인할 수 없습니다.";
            }

            return "xextool.exe 6.3만 지원합니다. 감지된 버전: " + detectedVersion;
        }

        private static bool IsTitleUpdatePath(string path)
        {
            if (String.IsNullOrEmpty(path) || !File.Exists(path) || IsIsoPath(path) || IsXexToolPath(path))
            {
                return false;
            }

            string extension = Path.GetExtension(path);
            string fileName = Path.GetFileName(path);
            if (String.Equals(extension, ".xexp", StringComparison.OrdinalIgnoreCase)
                || String.Equals(extension, ".tu", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("TU", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    byte[] magic = new byte[4];
                    if (stream.Read(magic, 0, magic.Length) != magic.Length)
                    {
                        return false;
                    }

                    string signature = Encoding.ASCII.GetString(magic);
                    return signature == "LIVE" || signature == "PIRS" || signature == "CON " || signature == "XEX2";
                }
            }
            catch
            {
                return false;
            }
        }

        private void SelectRelatedFiles(string[] paths)
        {
            int unsupportedCount = 0;
            string xexToolValidationMessage = null;
            for (int index = 0; index < paths.Length; index++)
            {
                string path = paths[index];
                if (IsIsoPath(path))
                {
                    selectedIsoPath = path;
                }
                else if (IsXexToolPath(path))
                {
                    string detectedVersion;
                    if (XexToolValidator.IsSupported(path, out detectedVersion))
                    {
                        selectedXexToolPath = path;
                    }
                    else
                    {
                        selectedXexToolPath = null;
                        xexToolValidationMessage = FormatUnsupportedXexToolMessage(detectedVersion);
                    }
                }
                else if (IsTitleUpdatePath(path))
                {
                    selectedTitleUpdatePath = path;
                }
                else
                {
                    unsupportedCount++;
                }
            }

            progressBar.Value = 0;
            UpdateFileStatusDisplay();
            int readyCount = (IsIsoPath(selectedIsoPath) ? 1 : 0)
                + (IsXexToolPath(selectedXexToolPath) ? 1 : 0)
                + (IsTitleUpdatePath(selectedTitleUpdatePath) ? 1 : 0);
            if (!String.IsNullOrEmpty(xexToolValidationMessage))
            {
                SetStatus(xexToolValidationMessage);
            }
            else if (unsupportedCount > 0)
            {
                SetStatus(String.Format("파일 {0}개 준비됨, 지원하지 않는 파일 {1}개 제외", readyCount, unsupportedCount));
            }
            else
            {
                SetStatus(String.Format("파일 {0}개 준비됨", readyCount));
            }
        }

        private void UpdateFileStatusDisplay()
        {
            SetFileStatus(isoStatusLabel, "(필수) 원본 ISO", selectedIsoPath, true);
            SetFileStatus(xexToolStatusLabel, "(필수) xextool.exe 6.3", selectedXexToolPath, true);
            SetFileStatus(titleUpdateStatusLabel, "(옵션) TU파일", selectedTitleUpdatePath, false);

            bool workerBusy = patchWorker != null && patchWorker.IsBusy;
            bool hasXexTool = IsXexToolPath(selectedXexToolPath);
            bool hasTitleUpdate = IsTitleUpdatePath(selectedTitleUpdatePath);
            if (applyTitleUpdateCheckBox != null)
            {
                applyTitleUpdateCheckBox.Enabled = hasXexTool && hasTitleUpdate && !workerBusy;
                if (!hasXexTool || !hasTitleUpdate)
                {
                    applyTitleUpdateCheckBox.Checked = false;
                }
            }

            if (specialAudition3AlwaysOpenCheckBox != null)
            {
                specialAudition3AlwaysOpenCheckBox.Enabled = hasXexTool && !workerBusy;
                if (!hasXexTool)
                {
                    specialAudition3AlwaysOpenCheckBox.Checked = false;
                }
            }

            if (activityStopMenuCheckBox != null)
            {
                activityStopMenuCheckBox.Enabled = hasXexTool && !workerBusy;
                if (!hasXexTool)
                {
                    activityStopMenuCheckBox.Checked = false;
                }
            }

            if (patchButton != null)
            {
                patchButton.Enabled = IsIsoPath(selectedIsoPath) && hasXexTool && !workerBusy;
            }
        }

        private static void SetFileStatus(Label label, string caption, string path, bool required)
        {
            if (label == null)
            {
                return;
            }

            bool ready = File.Exists(path);
            if (ready)
            {
                label.Text = "✅ " + caption + "  —  " + Path.GetFileName(path);
                label.ForeColor = Color.FromArgb(28, 128, 74);
            }
            else if (required)
            {
                label.Text = "❌ " + caption;
                label.ForeColor = Color.FromArgb(190, 55, 62);
            }
            else
            {
                label.Text = "○ " + caption;
                label.ForeColor = Color.FromArgb(104, 112, 126);
            }
        }

        private void OnPatchButtonClick(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(selectedIsoPath))
            {
                SetStatus("패치할 ISO를 먼저 선택해 주세요.");
                return;
            }

            string isoPath = selectedIsoPath;
            string xexToolPath = selectedXexToolPath;
            string titleUpdatePath = selectedTitleUpdatePath;
            bool applyTitleUpdate = applyTitleUpdateCheckBox != null && applyTitleUpdateCheckBox.Checked;
            bool unlockSpecialAudition3 = specialAudition3AlwaysOpenCheckBox != null && specialAudition3AlwaysOpenCheckBox.Checked;
            bool addActivityStopMenu = activityStopMenuCheckBox != null && activityStopMenuCheckBox.Checked;
            string detectedXexToolVersion;
            if (!XexToolValidator.IsSupported(xexToolPath, out detectedXexToolVersion))
            {
                selectedXexToolPath = null;
                UpdateFileStatusDisplay();
                SetStatus(FormatUnsupportedXexToolMessage(detectedXexToolVersion));
                return;
            }

            if (applyTitleUpdate && !IsTitleUpdatePath(titleUpdatePath))
            {
                SetStatus("타이틀 업데이트를 반영하려면 TU 파일을 먼저 선택해 주세요.");
                return;
            }

            bool doubleAuditionFans = doubleAuditionFansCheckBox != null && doubleAuditionFansCheckBox.Checked;
            bool ensureAuditionPassCount = ensureAuditionPassCountCheckBox != null && ensureAuditionPassCountCheckBox.Checked;
            bool doubleLessonGains = doubleLessonGainsCheckBox != null && doubleLessonGainsCheckBox.Checked;
            bool communicationPerfect = ShowCommunicationPerfectCheat && communicationPerfectCheckBox != null && communicationPerfectCheckBox.Checked;
            patchButton.Enabled = false;
            applyTitleUpdateCheckBox.Enabled = false;
            doubleAuditionFansCheckBox.Enabled = false;
            ensureAuditionPassCountCheckBox.Enabled = false;
            doubleLessonGainsCheckBox.Enabled = false;
            specialAudition3AlwaysOpenCheckBox.Enabled = false;
            activityStopMenuCheckBox.Enabled = false;
            communicationPerfectCheckBox.Enabled = false;
            dropPanel.Enabled = false;
            progressBar.Value = 0;
            SetStatus("패치 실행 중...");

            patchWorker = new BackgroundWorker();
            patchWorker.WorkerReportsProgress = true;
            patchWorker.DoWork += delegate(object workerSender, DoWorkEventArgs workerArgs)
            {
                RunIsoRoundTrip(
                    (BackgroundWorker)workerSender,
                    isoPath,
                    xexToolPath,
                    titleUpdatePath,
                    applyTitleUpdate,
                    doubleAuditionFans,
                    ensureAuditionPassCount,
                    doubleLessonGains,
                    unlockSpecialAudition3,
                    addActivityStopMenu,
                    communicationPerfect);
            };
            patchWorker.ProgressChanged += delegate(object workerSender, ProgressChangedEventArgs progressArgs)
            {
                progressBar.Value = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, progressArgs.ProgressPercentage));
                PatchProgressUpdate update = progressArgs.UserState as PatchProgressUpdate;
                if (update != null && update.AppendToLog)
                {
                    AppendStatus(update.Message);
                }
            };
            patchWorker.RunWorkerCompleted += delegate(object workerSender, RunWorkerCompletedEventArgs completedArgs)
            {
                dropPanel.Enabled = true;
                doubleAuditionFansCheckBox.Enabled = true;
                ensureAuditionPassCountCheckBox.Enabled = true;
                doubleLessonGainsCheckBox.Enabled = true;
                communicationPerfectCheckBox.Enabled = ShowCommunicationPerfectCheat;
                UpdateFileStatusDisplay();
                if (completedArgs.Error != null)
                {
                    AppendStatus("오류: " + completedArgs.Error.Message);
                    return;
                }
            };
            patchWorker.RunWorkerAsync();
        }

        private void RunIsoRoundTrip(
            BackgroundWorker worker,
            string isoPath,
            string xexToolPath,
            string titleUpdatePath,
            bool applyTitleUpdate,
            bool doubleAuditionFans,
            bool ensureAuditionPassCount,
            bool doubleLessonGains,
            bool unlockSpecialAudition3,
            bool addActivityStopMenu,
            bool communicationPerfect)
        {
            ReportStage(worker, 8, "입력 ISO 확인 중...");
            if (!File.Exists(isoPath))
            {
                throw new FileNotFoundException("선택한 ISO 파일이 존재하지 않습니다.", isoPath);
            }

            string assetRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            assetRoot = FindAssetsRoot(assetRoot);
            string exisoPath = Path.Combine(assetRoot, Path.Combine("Tools", "exiso.exe"));
            if (!File.Exists(exisoPath))
            {
                throw new FileNotFoundException("exiso.exe를 찾을 수 없습니다.", exisoPath);
            }

            string translationsPath = Path.Combine(assetRoot, "translations_text_id_ko.jsonl");
            if (!File.Exists(translationsPath))
            {
                throw new FileNotFoundException("번역 데이터를 찾을 수 없습니다.", translationsPath);
            }

            string defaultXexTranslationsPath = Path.Combine(assetRoot, "default_xex_text_id_ko.jsonl");
            if (!File.Exists(defaultXexTranslationsPath))
            {
                throw new FileNotFoundException("default.xex 번역 데이터를 찾을 수 없습니다.", defaultXexTranslationsPath);
            }

            string bxrTranslationsPath = Path.Combine(assetRoot, "bxr_texts.jsonl");
            if (!File.Exists(bxrTranslationsPath))
            {
                throw new FileNotFoundException("BXR 번역 데이터를 찾을 수 없습니다.", bxrTranslationsPath);
            }

            string remapPath = Path.Combine(assetRoot, Path.Combine("Remap", "xbox_hangul_remap.json"));
            if (!File.Exists(remapPath))
            {
                throw new FileNotFoundException("한글 remap 데이터를 찾을 수 없습니다.", remapPath);
            }

            string isoDirectory = Path.GetDirectoryName(isoPath);
            if (String.IsNullOrEmpty(isoDirectory))
            {
                isoDirectory = Environment.CurrentDirectory;
            }

            string baseName = Path.GetFileNameWithoutExtension(isoPath);
            string workRoot = GetAvailableDirectoryPath(Path.Combine(isoDirectory, baseName + "_patcher_work"));
            string extractRoot = Path.Combine(workRoot, "xiso_root");
            string outputIso = GetAvailableFilePath(Path.Combine(isoDirectory, baseName + "_repacked.iso"));

            Directory.CreateDirectory(extractRoot);

            ReportStage(worker, 20, "원본 ISO 해제 중...");
            RunTool(
                exisoPath,
                "-x -d " + QuoteArgument(extractRoot) + " " + QuoteArgument(isoPath),
                Path.GetDirectoryName(exisoPath));

            if (!Directory.Exists(extractRoot) || Directory.GetFileSystemEntries(extractRoot).Length == 0)
            {
                throw new InvalidOperationException("ISO 해제 결과가 비어 있습니다.");
            }

            bool titleUpdateApplied = false;
            if (applyTitleUpdate)
            {
                ReportStage(worker, 27, "타이틀 업데이트 반영 중...");
                TitleUpdatePatcher.Apply(
                    extractRoot,
                    xexToolPath,
                    titleUpdatePath,
                    workRoot,
                    delegate(int percent, string message)
                    {
                        Report(worker, percent, message);
                    });
                titleUpdateApplied = true;
            }

            ReportStage(worker, 32, "번역/remap 데이터 로드 중...");
            var translations = JsonTranslationStore.Load(translationsPath);
            var defaultXexTranslations = JsonTranslationStore.Load(defaultXexTranslationsPath);
            var bxrTranslations = BxrTextTranslationStore.Load(bxrTranslationsPath);
            HangulRemapper remapper = HangulRemapper.Load(remapPath);
            remapper.ValidateAll(translations.Values);
            remapper.ValidateAll(defaultXexTranslations.Values);
            remapper.ValidateAll(BxrTextTranslationStore.EnumerateTranslationTexts(bxrTranslations.Values));
            XboxTextPatcher textPatcher = new XboxTextPatcher(translations, remapper);

            ReportStage(worker, 35, "게임 텍스트 번역 중...");
            TranslationPatchResult patchResult = textPatcher.PatchExtractedRoot(
                extractRoot,
                delegate(int percent, string message)
                {
                    Report(worker, percent, message);
                });

            if (patchResult.MsgEntriesPatched == 0)
            {
                throw new InvalidOperationException("반영된 번역 문자열이 0개입니다.");
            }

            CreditLinePatchResult creditLineResult = null;
            /*
            Disabled: m0377 renders a fallback glyph in the credits screen.
            Keep this as a last-resort credit marker if no cleaner display path is found.
            creditLineResult = CreditLinePatcher.PatchExtractedRoot(
                extractRoot,
                remapper,
                delegate(int percent, string message)
                {
                    Report(worker, percent, message);
                });
            ValidateCreditLinePatch(creditLineResult);
            */

            CommunicationPerfectPatchResult communicationPerfectResult = null;
            if (communicationPerfect)
            {
                ReportStage(worker, 66, "영업 옵션 적용 중...");
                CommunicationPerfectPatcher communicationPatcher = CommunicationPerfectPatcher.Load(assetRoot);
                communicationPerfectResult = communicationPatcher.PatchExtractedRoot(
                    extractRoot,
                    delegate(int percent, string message)
                    {
                        Report(worker, percent, message);
                    });

                if (communicationPerfectResult.ManifestRows > 0 && communicationPerfectResult.ScbEntriesSeen == 0)
                {
                    throw new InvalidOperationException("영업 선택지 퍼펙트 패치 대상을 찾을 수 없습니다.");
                }

                if (communicationPerfectResult.ScoreValuesPatched == 0 && communicationPerfectResult.ScoreValuesAlreadyPerfect == 0)
                {
                    throw new InvalidOperationException("영업 선택지 퍼펙트에 반영된 점수가 0개입니다.");
                }

                if (communicationPerfectResult.MissingBnaFiles > 0 ||
                    communicationPerfectResult.MissingScbEntries > 0 ||
                    communicationPerfectResult.ScoreMismatches > 0 ||
                    communicationPerfectResult.InvalidRows > 0 ||
                    communicationPerfectResult.Errors > 0)
                {
                    throw new InvalidOperationException(
                        String.Format(
                            "영업 선택지 퍼펙트 패치 중 누락/오류가 있습니다. BNA {0:N0}, SCB {1:N0}, Mismatch {2:N0}, Invalid {3:N0}, Errors {4:N0}",
                            communicationPerfectResult.MissingBnaFiles,
                            communicationPerfectResult.MissingScbEntries,
                            communicationPerfectResult.ScoreMismatches,
                            communicationPerfectResult.InvalidRows,
                            communicationPerfectResult.Errors));
                }
            }

            ReportStage(worker, 70, "BXR 번역 중...");
            BxrTextPatcher bxrPatcher = new BxrTextPatcher(bxrTranslations, remapper);
            BxrPatchResult bxrResult = bxrPatcher.PatchExtractedRoot(
                extractRoot,
                delegate(int percent, string message)
                {
                    Report(worker, percent, message);
                });

            if (bxrTranslations.Count > 0 && bxrResult.StringsPatched == 0)
            {
                throw new InvalidOperationException("BXR에 반영된 문자열이 0개입니다.");
            }

            LessonGainPatchResult lessonGainResult = null;
            if (doubleLessonGains)
            {
                ReportStage(worker, 72, "레슨 능력치 상승 2배 적용 중...");
                LessonGainPatcher lessonGainPatcher = new LessonGainPatcher();
                lessonGainResult = lessonGainPatcher.PatchExtractedRoot(
                    extractRoot,
                    delegate(int percent, string message)
                    {
                        Report(worker, percent, message);
                    });

                if (!lessonGainResult.TargetBnaFound || lessonGainResult.TargetBxrEntriesSeen != 1)
                {
                    throw new InvalidOperationException("레슨 능력치 패치 대상을 찾을 수 없습니다.");
                }

                if (lessonGainResult.NonzeroValuesPatched == 0 && lessonGainResult.NonzeroValuesAlreadyPatched == 0)
                {
                    throw new InvalidOperationException("레슨 능력치 상승 2배에 반영된 필드가 0개입니다.");
                }
            }

            AuditionFanPatchResult auditionFanResult = null;
            if (doubleAuditionFans || ensureAuditionPassCount)
            {
                ReportStage(worker, 73, "오디션 옵션 적용 중...");
                AuditionFanPatcher auditionFanPatcher = new AuditionFanPatcher();
                auditionFanResult = auditionFanPatcher.PatchExtractedRoot(
                    extractRoot,
                    doubleAuditionFans,
                    ensureAuditionPassCount,
                    delegate(int percent, string message)
                    {
                        Report(worker, percent, message);
                    });

                if (auditionFanResult.TargetBxrFilesSeen == 0)
                {
                    throw new InvalidOperationException("오디션 팬 증가량 패치 대상을 찾을 수 없습니다.");
                }

                if (doubleAuditionFans && auditionFanResult.FanValuesPatched == 0 && auditionFanResult.FanValuesAlreadyPatched == 0)
                {
                    throw new InvalidOperationException("오디션 팬 증가량에 반영된 필드가 0개입니다.");
                }

                if (ensureAuditionPassCount && auditionFanResult.PassValuesPatched == 0 && auditionFanResult.PassValuesAlreadyAtLeastTwo == 0)
                {
                    throw new InvalidOperationException("오디션 합격자수에 반영된 필드가 0개입니다.");
                }
            }

            ReportStage(worker, 75, "이미지 리소스 적용 중...");
            ImageTexturePatcher imagePatcher = ImageTexturePatcher.Load(assetRoot);
            ImageTexturePatchResult imageResult = imagePatcher.PatchExtractedRoot(
                extractRoot,
                delegate(int percent, string message)
                {
                    Report(worker, percent, message);
                });

            int imageTexturesChanged = imageResult.EntriesPatched + imageResult.EntriesAdded;
            if (imageResult.ManifestRows > 0 && imageTexturesChanged == 0)
            {
                throw new InvalidOperationException("이미지에 반영된 텍스처가 0개입니다.");
            }

            if (imageResult.MissingAssets > 0 || imageResult.MissingBnaFiles > 0 || imageResult.MissingEntries > 0 || imageResult.Errors > 0)
            {
                throw new InvalidOperationException(
                    String.Format(
                        "이미지 패치 중 누락이 있습니다. Assets {0:N0}, BNA {1:N0}, Entry {2:N0}, Errors {3:N0}",
                        imageResult.MissingAssets,
                        imageResult.MissingBnaFiles,
                        imageResult.MissingEntries,
                        imageResult.Errors));
            }

            ReportStage(worker, 78, "XEX 번역 중...");
            XexTextPatcher xexPatcher = new XexTextPatcher(defaultXexTranslations, remapper);
            XexPatchResult xexResult = xexPatcher.PatchExtractedRoot(
                extractRoot,
                xexToolPath,
                workRoot,
                true,
                unlockSpecialAudition3,
                addActivityStopMenu,
                delegate(int percent, string message)
                {
                    Report(worker, percent, message);
                });

            if (unlockSpecialAudition3 &&
                xexResult.SpecialAudition3GatesPatched == 0 &&
                xexResult.SpecialAudition3GatesAlreadyPatched == 0)
            {
                throw new InvalidOperationException("특별 오디션 3 상시 개방 패치가 반영되지 않았습니다.");
            }

            if (addActivityStopMenu &&
                xexResult.ActivityStopMenusPatched == 0 &&
                xexResult.ActivityStopMenusAlreadyPatched == 0)
            {
                throw new InvalidOperationException("활동 중단 메뉴 패치가 반영되지 않았습니다.");
            }

            ReportStage(worker, 82, "폰트 적용 중...");
            FontPatchRunner.PatchExtractedRoot(
                extractRoot,
                assetRoot,
                workRoot,
                delegate(int percent, string message)
                {
                    Report(worker, percent, message);
                });

            ReportStage(worker, 89, "최종 리소스 정리 중...");
            BootLogoInfoPatchResult bootLogoInfoResult = BootLogoInfoPatcher.PatchExtractedRoot(
                extractRoot,
                assetRoot,
                delegate(int percent, string message)
                {
                    Report(worker, percent, message);
                });

            if (!bootLogoInfoResult.TargetBnaFound)
            {
                throw new InvalidOperationException("필수 리소스 파일을 찾을 수 없습니다.");
            }

            if (!bootLogoInfoResult.TargetEntryFound)
            {
                throw new InvalidOperationException("필수 리소스 항목을 찾을 수 없습니다.");
            }

            if (!bootLogoInfoResult.EndingEntriesFound)
            {
                throw new InvalidOperationException("엔딩 패치 정보 로고 항목을 찾을 수 없습니다.");
            }

            ReportStage(worker, 91, "번역된 파일로 ISO 재생성 중...");
            RunTool(
                exisoPath,
                "-c " + QuoteArgument(extractRoot) + " " + QuoteArgument(outputIso),
                Path.GetDirectoryName(exisoPath));

            if (!File.Exists(outputIso))
            {
                throw new FileNotFoundException("재생성된 ISO를 찾을 수 없습니다.", outputIso);
            }

            FileInfo outputInfo = new FileInfo(outputIso);
            if (outputInfo.Length == 0)
            {
                throw new InvalidOperationException("재생성된 ISO 파일 크기가 0입니다.");
            }

            ReportStage(worker, 99, "임시 작업 폴더 정리 중...");
            DeleteTemporaryWorkDirectory(workRoot, isoDirectory, baseName + "_patcher_work");

            ReportStage(
                worker,
                100,
                String.Format(
                    "완료: BNA {0:N0}개, BXR {1:N0}개, 이미지 {2:N0}개, XEX {3:N0}개 문자열 반영{4}{5}, {6}",
                    patchResult.MsgEntriesPatched,
                    bxrResult.StringsPatched,
                    imageTexturesChanged,
                    xexResult.StringsPatched,
                    FormatCreditLineSummary(creditLineResult),
                    FormatCommunicationPerfectSummary(communicationPerfectResult) + FormatAuditionFanSummary(auditionFanResult) + FormatLessonGainSummary(lessonGainResult) + FormatSpecialAudition3Summary(xexResult) + FormatActivityStopMenuSummary(xexResult) + FormatTitleUpdateSummary(titleUpdateApplied),
                    outputIso));
        }

        internal static void DeleteTemporaryWorkDirectory(string workRoot, string isoDirectory, string expectedName)
        {
            string fullWorkRoot = new DirectoryInfo(Path.GetFullPath(workRoot)).FullName;
            string fullIsoDirectory = new DirectoryInfo(Path.GetFullPath(isoDirectory)).FullName;
            DirectoryInfo workDirectory = new DirectoryInfo(fullWorkRoot);
            if (!workDirectory.Exists)
            {
                return;
            }

            DirectoryInfo parent = workDirectory.Parent;
            if (parent == null || !String.Equals(parent.FullName, fullIsoDirectory, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("임시 작업 폴더가 ISO 폴더 바로 아래에 있지 않습니다.");
            }

            if (!IsTemporaryWorkDirectoryName(workDirectory.Name, expectedName))
            {
                throw new InvalidOperationException("임시 작업 폴더 이름이 예상한 형식과 다릅니다.");
            }

            EnsureDirectoryTreeHasNoReparsePoints(workDirectory.FullName);
            Directory.Delete(workDirectory.FullName, true);
        }

        private static bool IsTemporaryWorkDirectoryName(string name, string expectedName)
        {
            if (String.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string numberedPrefix = expectedName + "_";
            if (!name.StartsWith(numberedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string suffix = name.Substring(numberedPrefix.Length);
            if (suffix.Length == 0)
            {
                return false;
            }

            for (int index = 0; index < suffix.Length; index++)
            {
                if (!Char.IsDigit(suffix[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void EnsureDirectoryTreeHasNoReparsePoints(string directoryPath)
        {
            if ((File.GetAttributes(directoryPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("임시 작업 폴더에 재분석 지점이 있어 안전하게 삭제할 수 없습니다.");
            }

            string[] entries = Directory.GetFileSystemEntries(directoryPath);
            for (int index = 0; index < entries.Length; index++)
            {
                FileAttributes attributes = File.GetAttributes(entries[index]);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("임시 작업 폴더에 재분석 지점이 있어 안전하게 삭제할 수 없습니다.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    EnsureDirectoryTreeHasNoReparsePoints(entries[index]);
                }
            }
        }

        private static void ValidateCreditLinePatch(CreditLinePatchResult result)
        {
            if (result == null || result.Errors > 0)
            {
                throw new InvalidOperationException("Credit line patch failed.");
            }

            if (!result.TargetBnaFound)
            {
                throw new InvalidOperationException("Credit line patch target initialFix.bna was not found.");
            }

            if (!result.TargetScbFound)
            {
                throw new InvalidOperationException("Credit line patch target f172_list_msg_etc.scb was not found.");
            }

            if (!result.TargetMsgFound)
            {
                throw new InvalidOperationException("Credit line patch target MSG m0377 was not found.");
            }

            if (result.StringsPatched == 0 && !result.AlreadyPatched)
            {
                throw new InvalidOperationException("Credit line patch did not change MSG m0377.");
            }
        }

        private static string FormatCreditLineSummary(CreditLinePatchResult result)
        {
            if (result == null)
            {
                return String.Empty;
            }

            if (result.StringsPatched > 0)
            {
                return ", 크레딧 1개 반영";
            }

            if (result.AlreadyPatched)
            {
                return ", 크레딧 이미 적용";
            }

            return String.Empty;
        }

        private static string FormatCommunicationPerfectSummary(CommunicationPerfectPatchResult result)
        {
            if (result == null)
            {
                return String.Empty;
            }

            if (result.ScoreValuesPatched > 0)
            {
                return String.Format(", 영업 퍼펙트 {0:N0}개 반영", result.ScoreValuesPatched);
            }

            if (result.ScoreValuesAlreadyPerfect > 0)
            {
                return String.Format(", 영업 퍼펙트 이미 적용 {0:N0}개", result.ScoreValuesAlreadyPerfect);
            }

            return String.Empty;
        }

        private static string FormatAuditionFanSummary(AuditionFanPatchResult result)
        {
            if (result == null)
            {
                return String.Empty;
            }

            StringBuilder builder = new StringBuilder();
            if (result.FanValuesPatched > 0)
            {
                builder.AppendFormat(", 오디션 팬 {0:N0}개 반영", result.FanValuesPatched);
            }
            else if (result.FanValuesAlreadyPatched > 0)
            {
                builder.AppendFormat(", 오디션 팬 이미 적용 {0:N0}개", result.FanValuesAlreadyPatched);
            }

            if (result.PassValuesPatched > 0)
            {
                builder.AppendFormat(", 합격자수 {0:N0}개 반영", result.PassValuesPatched);
            }
            else if (result.PassValuesAlreadyAtLeastTwo > 0)
            {
                builder.AppendFormat(", 합격자수 이미 2명이상 {0:N0}개", result.PassValuesAlreadyAtLeastTwo);
            }

            return builder.ToString();
        }

        private static string FormatLessonGainSummary(LessonGainPatchResult result)
        {
            if (result == null)
            {
                return String.Empty;
            }

            if (result.NonzeroValuesPatched > 0)
            {
                return String.Format(", 레슨 능력치 상승 2배 {0:N0}개 반영", result.NonzeroValuesPatched);
            }

            if (result.NonzeroValuesAlreadyPatched > 0)
            {
                return String.Format(", 레슨 능력치 상승 2배 이미 적용 {0:N0}개", result.NonzeroValuesAlreadyPatched);
            }

            return String.Empty;
        }

        private static string FormatSpecialAudition3Summary(XexPatchResult result)
        {
            if (result == null)
            {
                return String.Empty;
            }

            if (result.SpecialAudition3GatesPatched > 0)
            {
                return ", 특별 오디션 3 상시 개방";
            }

            if (result.SpecialAudition3GatesAlreadyPatched > 0)
            {
                return ", 특별 오디션 3 이미 상시 개방";
            }

            return String.Empty;
        }

        private static string FormatActivityStopMenuSummary(XexPatchResult result)
        {
            if (result == null)
            {
                return String.Empty;
            }

            if (result.ActivityStopMenusPatched > 0)
            {
                return ", 활동 중단 메뉴 추가";
            }

            if (result.ActivityStopMenusAlreadyPatched > 0)
            {
                return ", 활동 중단 메뉴 이미 적용";
            }

            return String.Empty;
        }

        private static string FormatTitleUpdateSummary(bool applied)
        {
            return applied ? ", 타이틀 업데이트 반영" : String.Empty;
        }

        private static string FindAssetsRoot(string preferredPath)
        {
            if (Directory.Exists(preferredPath))
            {
                return preferredPath;
            }

            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return preferredPath;
        }

        private static string GetAvailableDirectoryPath(string desiredPath)
        {
            if (!Directory.Exists(desiredPath) && !File.Exists(desiredPath))
            {
                return desiredPath;
            }

            for (int i = 1; i < 1000; i++)
            {
                string candidate = desiredPath + "_" + i.ToString();
                if (!Directory.Exists(candidate) && !File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new IOException("사용 가능한 작업 폴더 이름을 찾을 수 없습니다.");
        }

        private static string GetAvailableFilePath(string desiredPath)
        {
            if (!File.Exists(desiredPath) && !Directory.Exists(desiredPath))
            {
                return desiredPath;
            }

            string directory = Path.GetDirectoryName(desiredPath);
            if (String.IsNullOrEmpty(directory))
            {
                directory = Environment.CurrentDirectory;
            }

            string name = Path.GetFileNameWithoutExtension(desiredPath);
            string extension = Path.GetExtension(desiredPath);
            for (int i = 1; i < 1000; i++)
            {
                string candidate = Path.Combine(directory, name + "_" + i.ToString() + extension);
                if (!File.Exists(candidate) && !Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new IOException("사용 가능한 출력 ISO 이름을 찾을 수 없습니다.");
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void RunTool(string fileName, string arguments, string workingDirectory)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = fileName;
            startInfo.Arguments = arguments;
            startInfo.WorkingDirectory = workingDirectory;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            StringBuilder output = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                output.Append(process.StandardOutput.ReadToEnd());
                output.Append(process.StandardError.ReadToEnd());
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string message = output.ToString().Trim();
                    if (message.Length == 0)
                    {
                        message = "exit code " + process.ExitCode.ToString();
                    }
                    throw new InvalidOperationException("exiso.exe 실행 실패: " + message);
                }
            }
        }

        private static void Report(BackgroundWorker worker, int percent, string message)
        {
            worker.ReportProgress(percent, new PatchProgressUpdate(message, false));
        }

        private static void ReportStage(BackgroundWorker worker, int percent, string message)
        {
            worker.ReportProgress(percent, new PatchProgressUpdate(message, true));
        }

        private void SetStatus(string message)
        {
            statusTextBox.Text = message;
            statusTextBox.SelectionStart = statusTextBox.TextLength;
            statusTextBox.SelectionLength = 0;
            statusTextBox.ScrollToCaret();
        }

        private void AppendStatus(string message)
        {
            if (String.IsNullOrEmpty(message))
            {
                return;
            }

            string[] lines = statusTextBox.Lines;
            if (lines.Length > 0 && String.Equals(lines[lines.Length - 1], message, StringComparison.Ordinal))
            {
                return;
            }

            if (statusTextBox.TextLength > 0)
            {
                statusTextBox.AppendText(Environment.NewLine);
            }
            statusTextBox.AppendText(message);
            statusTextBox.SelectionStart = statusTextBox.TextLength;
            statusTextBox.SelectionLength = 0;
            statusTextBox.ScrollToCaret();
        }
    }
}
