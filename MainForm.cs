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
        private CheckBox doubleAuditionFansCheckBox;
        private CheckBox ensureAuditionPassCountCheckBox;
        private CheckBox communicationPerfectCheckBox;
        private Button patchButton;
        private ProgressBar progressBar;
        private TextBox statusTextBox;

        private string selectedIsoPath;
        private BackgroundWorker patchWorker;

        public MainForm()
        {
            Text = "\uc544\uc774\ub3cc\ub9c8\uc2a4\ud130 \ud55c\uae00 \ud328\uce58";
            Font = new Font("Malgun Gothic", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(246, 247, 250);
            MinimumSize = new Size(720, 360);
            Size = new Size(840, 420);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;

            TableLayoutPanel root = new TableLayoutPanel();
            root.ColumnCount = 5;
            root.RowCount = 3;
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(18);
            root.BackColor = BackColor;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            Controls.Add(root);

            dropPanel = BuildDropPanel();
            root.Controls.Add(dropPanel, 0, 0);

            root.Controls.Add(BuildVerticalSeparator(), 1, 0);

            Panel cheatPanel = BuildCheatPanel();
            root.Controls.Add(cheatPanel, 2, 0);

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
            statusTextBox.BorderStyle = BorderStyle.None;
            statusTextBox.BackColor = BackColor;
            statusTextBox.ForeColor = Color.FromArgb(65, 72, 86);
            statusTextBox.ReadOnly = true;
            statusTextBox.ShortcutsEnabled = true;
            statusTextBox.TabStop = true;
            statusTextBox.Text = "\ub300\uae30 \uc911";
            root.Controls.Add(statusTextBox, 0, 2);
            root.SetColumnSpan(statusTextBox, 5);

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
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
            inner.RowCount = 3;
            inner.Dock = DockStyle.Fill;
            inner.Padding = new Padding(22);
            inner.BackColor = Color.White;
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            panel.Controls.Add(inner);

            TableLayoutPanel center = new TableLayoutPanel();
            center.ColumnCount = 1;
            center.RowCount = 2;
            center.Dock = DockStyle.Fill;
            center.BackColor = Color.White;
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            inner.Controls.Add(center, 0, 1);

            dropTitleLabel = new Label();
            dropTitleLabel.Dock = DockStyle.Fill;
            dropTitleLabel.TextAlign = ContentAlignment.MiddleCenter;
            dropTitleLabel.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold, GraphicsUnit.Point);
            dropTitleLabel.ForeColor = Color.FromArgb(34, 42, 54);
            dropTitleLabel.AutoSize = false;
            dropTitleLabel.UseMnemonic = false;
            dropTitleLabel.Text = "\uc6d0\ubcf8 ISO\ub97c \uc5ec\uae30\uc5d0\r\n\ub4dc\ub798\uadf8 & \ub4dc\ub86d";
            center.Controls.Add(dropTitleLabel, 0, 0);

            dropHintLabel = new Label();
            dropHintLabel.Dock = DockStyle.Fill;
            dropHintLabel.TextAlign = ContentAlignment.TopCenter;
            dropHintLabel.AutoEllipsis = true;
            dropHintLabel.UseMnemonic = false;
            dropHintLabel.ForeColor = Color.FromArgb(92, 101, 116);
            dropHintLabel.Text = "\ud074\ub9ad\ud574\uc11c ISO \ud30c\uc77c \uc120\ud0dd";
            center.Controls.Add(dropHintLabel, 0, 1);

            panel.Click += OnDropPanelClick;
            inner.Click += OnDropPanelClick;
            center.Click += OnDropPanelClick;
            dropTitleLabel.Click += OnDropPanelClick;
            dropHintLabel.Click += OnDropPanelClick;
            panel.DragEnter += OnDragEnter;
            panel.DragDrop += OnDragDrop;
            inner.DragEnter += OnDragEnter;
            inner.DragDrop += OnDragDrop;
            center.DragEnter += OnDragEnter;
            center.DragDrop += OnDragDrop;

            return panel;
        }

        private Panel BuildVerticalSeparator()
        {
            Panel separator = new Panel();
            separator.BackColor = Color.FromArgb(205, 211, 222);
            separator.Dock = DockStyle.Fill;
            separator.Margin = new Padding(0, 8, 0, 22);
            return separator;
        }

        private Panel BuildCheatPanel()
        {
            Panel panel = new Panel();
            panel.BackColor = BackColor;
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(16, 0, 12, 14);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.ColumnCount = 1;
            layout.RowCount = 6;
            layout.Dock = DockStyle.Fill;
            layout.BackColor = BackColor;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ShowCommunicationPerfectCheat ? 34F : 0F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            panel.Controls.Add(layout);

            Label cheatLabel = new Label();
            cheatLabel.Dock = DockStyle.Fill;
            cheatLabel.Text = "\uce58\ud2b8";
            cheatLabel.TextAlign = ContentAlignment.MiddleLeft;
            cheatLabel.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold, GraphicsUnit.Point);
            cheatLabel.ForeColor = Color.FromArgb(34, 42, 54);
            cheatLabel.Margin = new Padding(0);
            layout.Controls.Add(cheatLabel, 0, 1);

            doubleAuditionFansCheckBox = new CheckBox();
            doubleAuditionFansCheckBox.Anchor = AnchorStyles.Left;
            doubleAuditionFansCheckBox.AutoSize = true;
            doubleAuditionFansCheckBox.Text = "\uc624\ub514\uc158 \ud32c \uc99d\uac00\ub7c9 2\ubc30";
            doubleAuditionFansCheckBox.ForeColor = Color.FromArgb(65, 72, 86);
            doubleAuditionFansCheckBox.Margin = new Padding(0);
            layout.Controls.Add(doubleAuditionFansCheckBox, 0, 2);

            ensureAuditionPassCountCheckBox = new CheckBox();
            ensureAuditionPassCountCheckBox.Anchor = AnchorStyles.Left;
            ensureAuditionPassCountCheckBox.AutoSize = true;
            ensureAuditionPassCountCheckBox.Text = "\uc624\ub514\uc158\uc758 \ud569\uaca9\uc790\uc218 2\uba85\uc774\uc0c1\uc73c\ub85c \ubcc0\uacbd";
            ensureAuditionPassCountCheckBox.ForeColor = Color.FromArgb(65, 72, 86);
            ensureAuditionPassCountCheckBox.Margin = new Padding(0);
            layout.Controls.Add(ensureAuditionPassCountCheckBox, 0, 3);

            communicationPerfectCheckBox = new CheckBox();
            communicationPerfectCheckBox.Anchor = AnchorStyles.Left;
            communicationPerfectCheckBox.AutoSize = true;
            communicationPerfectCheckBox.Text = "\uc601\uc5c5 \uc120\ud0dd\uc9c0 \ud56d\uc0c1 \ud37c\ud399\ud2b8";
            communicationPerfectCheckBox.ForeColor = Color.FromArgb(65, 72, 86);
            communicationPerfectCheckBox.Margin = new Padding(0);
            communicationPerfectCheckBox.Visible = ShowCommunicationPerfectCheat;
            communicationPerfectCheckBox.TabStop = ShowCommunicationPerfectCheat;
            layout.Controls.Add(communicationPerfectCheckBox, 0, 4);

            return panel;
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
            patchButton.Text = "\ud55c\uae00 \ud328\uce58";
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
                dialog.Title = "\uc6d0\ubcf8 ISO \ud30c\uc77c \uc120\ud0dd";
                dialog.Filter = "ISO files (*.iso)|*.iso|All files (*.*)|*.*";
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    SelectIso(dialog.FileName);
                }
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            string path = TryGetDroppedIso(e);
            e.Effect = path == null ? DragDropEffects.None : DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string path = TryGetDroppedIso(e);
            if (path == null)
            {
                SetStatus("\uc120\ud0dd\ud55c \ud30c\uc77c\uc740 ISO\uac00 \uc544\ub2d9\ub2c8\ub2e4.");
                return;
            }

            SelectIso(path);
        }

        private static string TryGetDroppedIso(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return null;
            }

            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length != 1)
            {
                return null;
            }

            string path = files[0];
            if (!IsIsoPath(path))
            {
                return null;
            }

            return path;
        }

        private static bool IsIsoPath(string path)
        {
            return !String.IsNullOrEmpty(path)
                && File.Exists(path)
                && String.Equals(Path.GetExtension(path), ".iso", StringComparison.OrdinalIgnoreCase);
        }

        private void SelectIso(string path)
        {
            if (!IsIsoPath(path))
            {
                SetStatus("ISO \ud30c\uc77c\ub9cc \uc120\ud0dd\ud560 \uc218 \uc788\uc2b5\ub2c8\ub2e4.");
                return;
            }

            selectedIsoPath = path;
            patchButton.Enabled = true;
            progressBar.Value = 0;
            dropHintLabel.Text = Path.GetFileName(path);
            SetStatus("ISO \uc120\ud0dd\ub428: " + path);
        }

        private void OnPatchButtonClick(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(selectedIsoPath))
            {
                SetStatus("\ud328\uce58\ud560 ISO\ub97c \uba3c\uc800 \uc120\ud0dd\ud574 \uc8fc\uc138\uc694.");
                return;
            }

            string isoPath = selectedIsoPath;
            bool doubleAuditionFans = doubleAuditionFansCheckBox != null && doubleAuditionFansCheckBox.Checked;
            bool ensureAuditionPassCount = ensureAuditionPassCountCheckBox != null && ensureAuditionPassCountCheckBox.Checked;
            bool communicationPerfect = ShowCommunicationPerfectCheat && communicationPerfectCheckBox != null && communicationPerfectCheckBox.Checked;
            patchButton.Enabled = false;
            doubleAuditionFansCheckBox.Enabled = false;
            ensureAuditionPassCountCheckBox.Enabled = false;
            communicationPerfectCheckBox.Enabled = false;
            dropPanel.Enabled = false;
            progressBar.Value = 0;
            SetStatus("\ud328\uce58 \uc2e4\ud589 \uc911...");

            patchWorker = new BackgroundWorker();
            patchWorker.WorkerReportsProgress = true;
            patchWorker.DoWork += delegate(object workerSender, DoWorkEventArgs workerArgs)
            {
                RunIsoRoundTrip((BackgroundWorker)workerSender, isoPath, doubleAuditionFans, ensureAuditionPassCount, communicationPerfect);
            };
            patchWorker.ProgressChanged += delegate(object workerSender, ProgressChangedEventArgs progressArgs)
            {
                progressBar.Value = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, progressArgs.ProgressPercentage));
                if (progressArgs.UserState is string)
                {
                    SetStatus((string)progressArgs.UserState);
                }
            };
            patchWorker.RunWorkerCompleted += delegate(object workerSender, RunWorkerCompletedEventArgs completedArgs)
            {
                dropPanel.Enabled = true;
                patchButton.Enabled = !String.IsNullOrEmpty(selectedIsoPath);
                doubleAuditionFansCheckBox.Enabled = true;
                ensureAuditionPassCountCheckBox.Enabled = true;
                communicationPerfectCheckBox.Enabled = true;
                if (completedArgs.Error != null)
                {
                    SetStatus("\uc624\ub958: " + completedArgs.Error.Message);
                    return;
                }
            };
            patchWorker.RunWorkerAsync();
        }

        private void RunIsoRoundTrip(
            BackgroundWorker worker,
            string isoPath,
            bool doubleAuditionFans,
            bool ensureAuditionPassCount,
            bool communicationPerfect)
        {
            Report(worker, 8, "\uc785\ub825 ISO \ud655\uc778 \uc911...");
            if (!File.Exists(isoPath))
            {
                throw new FileNotFoundException("\uc120\ud0dd\ud55c ISO \ud30c\uc77c\uc774 \uc874\uc7ac\ud558\uc9c0 \uc54a\uc2b5\ub2c8\ub2e4.", isoPath);
            }

            string assetRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            assetRoot = FindAssetsRoot(assetRoot);
            string exisoPath = Path.Combine(assetRoot, Path.Combine("Tools", "exiso.exe"));
            if (!File.Exists(exisoPath))
            {
                throw new FileNotFoundException("exiso.exe\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", exisoPath);
            }

            string translationsPath = Path.Combine(assetRoot, "translations_text_id_ko.jsonl");
            if (!File.Exists(translationsPath))
            {
                throw new FileNotFoundException("\ubc88\uc5ed \ub370\uc774\ud130\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", translationsPath);
            }

            string defaultXexTranslationsPath = Path.Combine(assetRoot, "default_xex_text_id_ko.jsonl");
            if (!File.Exists(defaultXexTranslationsPath))
            {
                throw new FileNotFoundException("default.xex \ubc88\uc5ed \ub370\uc774\ud130\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", defaultXexTranslationsPath);
            }

            string bxrTranslationsPath = Path.Combine(assetRoot, "bxr_texts.jsonl");
            if (!File.Exists(bxrTranslationsPath))
            {
                throw new FileNotFoundException("BXR \ubc88\uc5ed \ub370\uc774\ud130\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", bxrTranslationsPath);
            }

            string remapPath = Path.Combine(assetRoot, Path.Combine("Remap", "xbox_hangul_remap.json"));
            if (!File.Exists(remapPath))
            {
                throw new FileNotFoundException("\ud55c\uae00 remap \ub370\uc774\ud130\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", remapPath);
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

            Report(worker, 20, "\uc6d0\ubcf8 ISO \ud574\uc81c \uc911...");
            RunTool(
                exisoPath,
                "-x -d " + QuoteArgument(extractRoot) + " " + QuoteArgument(isoPath),
                Path.GetDirectoryName(exisoPath));

            if (!Directory.Exists(extractRoot) || Directory.GetFileSystemEntries(extractRoot).Length == 0)
            {
                throw new InvalidOperationException("ISO \ud574\uc81c \uacb0\uacfc\uac00 \ube44\uc5b4 \uc788\uc2b5\ub2c8\ub2e4.");
            }

            Report(worker, 32, "\ubc88\uc5ed/remap \ub370\uc774\ud130 \ub85c\ub4dc \uc911...");
            var translations = JsonTranslationStore.Load(translationsPath);
            var defaultXexTranslations = JsonTranslationStore.Load(defaultXexTranslationsPath);
            var bxrTranslations = BxrTextTranslationStore.Load(bxrTranslationsPath);
            HangulRemapper remapper = HangulRemapper.Load(remapPath);
            remapper.ValidateAll(translations.Values);
            remapper.ValidateAll(defaultXexTranslations.Values);
            remapper.ValidateAll(bxrTranslations.Values);
            XboxTextPatcher textPatcher = new XboxTextPatcher(translations, remapper);

            Report(worker, 35, "\ud574\uc81c\ub41c \ud30c\uc77c\uc5d0 \ubc88\uc5ed \ubc18\uc601 \uc911...");
            TranslationPatchResult patchResult = textPatcher.PatchExtractedRoot(
                extractRoot,
                delegate(int percent, string message)
                {
                    Report(worker, percent, message);
                });

            if (patchResult.MsgEntriesPatched == 0)
            {
                throw new InvalidOperationException("\ubc18\uc601\ub41c \ubc88\uc5ed \ubb38\uc790\uc5f4\uc774 0\uac1c\uc785\ub2c8\ub2e4.");
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
                CommunicationPerfectPatcher communicationPatcher = CommunicationPerfectPatcher.Load(assetRoot);
                communicationPerfectResult = communicationPatcher.PatchExtractedRoot(
                    extractRoot,
                    delegate(int percent, string message)
                    {
                        Report(worker, percent, message);
                    });

                if (communicationPerfectResult.ManifestRows > 0 && communicationPerfectResult.ScbEntriesSeen == 0)
                {
                    throw new InvalidOperationException("\uc601\uc5c5 \uc120\ud0dd\uc9c0 \ud37c\ud399\ud2b8 \ud328\uce58 \ub300\uc0c1\uc744 \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.");
                }

                if (communicationPerfectResult.ScoreValuesPatched == 0 && communicationPerfectResult.ScoreValuesAlreadyPerfect == 0)
                {
                    throw new InvalidOperationException("\uc601\uc5c5 \uc120\ud0dd\uc9c0 \ud37c\ud399\ud2b8\uc5d0 \ubc18\uc601\ub41c \uc810\uc218\uac00 0\uac1c\uc785\ub2c8\ub2e4.");
                }

                if (communicationPerfectResult.MissingBnaFiles > 0 ||
                    communicationPerfectResult.MissingScbEntries > 0 ||
                    communicationPerfectResult.ScoreMismatches > 0 ||
                    communicationPerfectResult.InvalidRows > 0 ||
                    communicationPerfectResult.Errors > 0)
                {
                    throw new InvalidOperationException(
                        String.Format(
                            "\uc601\uc5c5 \uc120\ud0dd\uc9c0 \ud37c\ud399\ud2b8 \ud328\uce58 \uc911 \ub204\ub77d/\uc624\ub958\uac00 \uc788\uc2b5\ub2c8\ub2e4. BNA {0:N0}, SCB {1:N0}, Mismatch {2:N0}, Invalid {3:N0}, Errors {4:N0}",
                            communicationPerfectResult.MissingBnaFiles,
                            communicationPerfectResult.MissingScbEntries,
                            communicationPerfectResult.ScoreMismatches,
                            communicationPerfectResult.InvalidRows,
                            communicationPerfectResult.Errors));
                }
            }

            BxrTextPatcher bxrPatcher = new BxrTextPatcher(bxrTranslations, remapper);
            BxrPatchResult bxrResult = bxrPatcher.PatchExtractedRoot(
                extractRoot,
                delegate(int percent, string message)
                {
                    Report(worker, percent, message);
                });

            if (bxrTranslations.Count > 0 && bxrResult.StringsPatched == 0)
            {
                throw new InvalidOperationException("BXR\uc5d0 \ubc18\uc601\ub41c \ubb38\uc790\uc5f4\uc774 0\uac1c\uc785\ub2c8\ub2e4.");
            }

            AuditionFanPatchResult auditionFanResult = null;
            if (doubleAuditionFans || ensureAuditionPassCount)
            {
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
                    throw new InvalidOperationException("\uc624\ub514\uc158 \ud32c \uc99d\uac00\ub7c9 \ud328\uce58 \ub300\uc0c1\uc744 \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.");
                }

                if (doubleAuditionFans && auditionFanResult.FanValuesPatched == 0 && auditionFanResult.FanValuesAlreadyPatched == 0)
                {
                    throw new InvalidOperationException("\uc624\ub514\uc158 \ud32c \uc99d\uac00\ub7c9\uc5d0 \ubc18\uc601\ub41c \ud544\ub4dc\uac00 0\uac1c\uc785\ub2c8\ub2e4.");
                }

                if (ensureAuditionPassCount && auditionFanResult.PassValuesPatched == 0 && auditionFanResult.PassValuesAlreadyAtLeastTwo == 0)
                {
                    throw new InvalidOperationException("\uc624\ub514\uc158 \ud569\uaca9\uc790\uc218\uc5d0 \ubc18\uc601\ub41c \ud544\ub4dc\uac00 0\uac1c\uc785\ub2c8\ub2e4.");
                }
            }

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
                throw new InvalidOperationException("\uc774\ubbf8\uc9c0\uc5d0 \ubc18\uc601\ub41c \ud14d\uc2a4\ucc98\uac00 0\uac1c\uc785\ub2c8\ub2e4.");
            }

            if (imageResult.MissingAssets > 0 || imageResult.MissingBnaFiles > 0 || imageResult.MissingEntries > 0 || imageResult.Errors > 0)
            {
                throw new InvalidOperationException(
                    String.Format(
                        "\uc774\ubbf8\uc9c0 \ud328\uce58 \uc911 \ub204\ub77d\uc774 \uc788\uc2b5\ub2c8\ub2e4. Assets {0:N0}, BNA {1:N0}, Entry {2:N0}, Errors {3:N0}",
                        imageResult.MissingAssets,
                        imageResult.MissingBnaFiles,
                        imageResult.MissingEntries,
                        imageResult.Errors));
            }

            XexTextPatcher xexPatcher = new XexTextPatcher(defaultXexTranslations, remapper);
            XexPatchResult xexResult = xexPatcher.PatchExtractedRoot(
                extractRoot,
                assetRoot,
                workRoot,
                delegate(int percent, string message)
                {
                    Report(worker, percent, message);
                });

            FontPatchRunner.PatchExtractedRoot(
                extractRoot,
                assetRoot,
                workRoot,
                delegate(int percent, string message)
                {
                    Report(worker, percent, message);
                });

            Report(worker, 91, "\ubc88\uc5ed\ub41c \ud30c\uc77c\ub85c ISO \uc7ac\uc0dd\uc131 \uc911...");
            RunTool(
                exisoPath,
                "-c " + QuoteArgument(extractRoot) + " " + QuoteArgument(outputIso),
                Path.GetDirectoryName(exisoPath));

            if (!File.Exists(outputIso))
            {
                throw new FileNotFoundException("\uc7ac\uc0dd\uc131\ub41c ISO\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", outputIso);
            }

            FileInfo outputInfo = new FileInfo(outputIso);
            if (outputInfo.Length == 0)
            {
                throw new InvalidOperationException("\uc7ac\uc0dd\uc131\ub41c ISO \ud30c\uc77c \ud06c\uae30\uac00 0\uc785\ub2c8\ub2e4.");
            }

            Report(
                worker,
                100,
                String.Format(
                    "\uc644\ub8cc: BNA {0:N0}\uac1c, BXR {1:N0}\uac1c, \uc774\ubbf8\uc9c0 {2:N0}\uac1c, XEX {3:N0}\uac1c \ubb38\uc790\uc5f4 \ubc18\uc601{4}{5}, {6}",
                    patchResult.MsgEntriesPatched,
                    bxrResult.StringsPatched,
                    imageTexturesChanged,
                    xexResult.StringsPatched,
                    FormatCreditLineSummary(creditLineResult),
                    FormatCommunicationPerfectSummary(communicationPerfectResult) + FormatAuditionFanSummary(auditionFanResult),
                    outputIso));
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
                return ", \ud06c\ub808\ub527 1\uac1c \ubc18\uc601";
            }

            if (result.AlreadyPatched)
            {
                return ", \ud06c\ub808\ub527 \uc774\ubbf8 \uc801\uc6a9";
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
                return String.Format(", \uc601\uc5c5 \ud37c\ud399\ud2b8 {0:N0}\uac1c \ubc18\uc601", result.ScoreValuesPatched);
            }

            if (result.ScoreValuesAlreadyPerfect > 0)
            {
                return String.Format(", \uc601\uc5c5 \ud37c\ud399\ud2b8 \uc774\ubbf8 \uc801\uc6a9 {0:N0}\uac1c", result.ScoreValuesAlreadyPerfect);
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
                builder.AppendFormat(", \uc624\ub514\uc158 \ud32c {0:N0}\uac1c \ubc18\uc601", result.FanValuesPatched);
            }
            else if (result.FanValuesAlreadyPatched > 0)
            {
                builder.AppendFormat(", \uc624\ub514\uc158 \ud32c \uc774\ubbf8 \uc801\uc6a9 {0:N0}\uac1c", result.FanValuesAlreadyPatched);
            }

            if (result.PassValuesPatched > 0)
            {
                builder.AppendFormat(", \ud569\uaca9\uc790\uc218 {0:N0}\uac1c \ubc18\uc601", result.PassValuesPatched);
            }
            else if (result.PassValuesAlreadyAtLeastTwo > 0)
            {
                builder.AppendFormat(", \ud569\uaca9\uc790\uc218 \uc774\ubbf8 2\uba85\uc774\uc0c1 {0:N0}\uac1c", result.PassValuesAlreadyAtLeastTwo);
            }

            return builder.ToString();
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

            throw new IOException("\uc0ac\uc6a9 \uac00\ub2a5\ud55c \uc791\uc5c5 \ud3f4\ub354 \uc774\ub984\uc744 \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.");
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

            throw new IOException("\uc0ac\uc6a9 \uac00\ub2a5\ud55c \ucd9c\ub825 ISO \uc774\ub984\uc744 \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.");
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
                    throw new InvalidOperationException("exiso.exe \uc2e4\ud589 \uc2e4\ud328: " + message);
                }
            }
        }

        private static void Report(BackgroundWorker worker, int percent, string message)
        {
            worker.ReportProgress(percent, message);
        }

        private void SetStatus(string message)
        {
            statusTextBox.Text = message;
            statusTextBox.SelectionStart = 0;
            statusTextBox.SelectionLength = 0;
        }
    }
}
