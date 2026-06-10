using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AUHealthIdGenerator
{
    public partial class MainForm : Form
    {
        // ─── Theme ────────────────────────────────────────────────────────────
        private static readonly Color ColBackground    = Color.FromArgb(245, 247, 250);
        private static readonly Color ColSurface       = Color.White;
        private static readonly Color ColPrimary       = Color.FromArgb(0, 102, 204);
        private static readonly Color ColPrimaryHover  = Color.FromArgb(0, 82, 170);
        private static readonly Color ColAccent        = Color.FromArgb(0, 168, 120);
        private static readonly Color ColDanger        = Color.FromArgb(210, 50, 50);
        private static readonly Color ColBorder        = Color.FromArgb(213, 219, 229);
        private static readonly Color ColTextPrimary   = Color.FromArgb(22, 28, 40);
        private static readonly Color ColTextSecondary = Color.FromArgb(100, 110, 130);
        private static readonly Color ColTabActive     = Color.FromArgb(0, 102, 204);
        private static readonly Color ColTabInactive   = Color.FromArgb(160, 175, 200);
        private static readonly Color ColBadgeValid    = Color.FromArgb(220, 248, 235);
        private static readonly Color ColBadgeInvalid  = Color.FromArgb(255, 235, 235);
        private static readonly Color ColRowAlt        = Color.FromArgb(248, 250, 253);
        private static readonly Color ColRowHover      = Color.FromArgb(237, 244, 255);
        private static readonly Color ColCopyBtn       = Color.FromArgb(210, 228, 252);

        private static readonly Font FontTitle   = new("Segoe UI", 17f, FontStyle.Bold);
        private static readonly Font FontHeading = new("Segoe UI", 10.5f, FontStyle.Bold);
        private static readonly Font FontBody    = new("Segoe UI", 9.5f);
        private static readonly Font FontMono    = new("Consolas", 10f);
        private static readonly Font FontSmall   = new("Segoe UI", 8.5f);
        private static readonly Font FontLabel   = new("Segoe UI", 9f);
        private static readonly Font FontBold    = new("Segoe UI", 9.5f, FontStyle.Bold);

        // Left panel width
        private const int LeftWidth = 430;

        // ─── Generator state ─────────────────────────────────────────────────
        private List<string> _generatedNumbers       = new();
        private HealthIdType _selectedType           = HealthIdType.HPI_I;
        private int          _hoverRowIndex          = -1;
        private int          _copiedRowIndex         = -1;
        private System.Windows.Forms.Timer? _copyFlashTimer;

        // ─── UI refs ─────────────────────────────────────────────────────────
        private Button        _btnTabGen   = null!;
        private Button        _btnTabVal   = null!;
        private Panel         _genPanel    = null!;
        private Panel         _valPanel    = null!;

        // Generator
        private Panel         _pnlTypeHpii  = null!;
        private Panel         _pnlTypeHpio  = null!;
        private Panel         _pnlTypeIhi   = null!;
        private Panel         _pnlTypeMed   = null!;
        private Panel         _pnlTypeProv  = null!;
        private Panel         _pnlProvOpts  = null!;
        private RadioButton   _rdoRandom    = null!;
        private RadioButton   _rdoState     = null!;
        private ComboBox      _cmbState     = null!;
        private NumericUpDown _nudCount     = null!;
        private Button        _btnGenerate  = null!;
        private ListBox       _lstResults   = null!;
        private Button        _btnCopyAll   = null!;
        private Button        _btnExportCsv = null!;
        private Button        _btnClearGen  = null!;
        private Label         _lblGenStatus = null!;

        // Validator
        private TextBox _txtValidate    = null!;
        private Button  _btnValidate    = null!;
        private Panel   _pnlValidResult = null!;
        private Label   _lblBadge       = null!;
        private Label   _lblValType     = null!;
        private Label   _lblValDesc     = null!;
        private Panel   _pnlCheckRows   = null!;
        private Button  _btnClearVal    = null!;

        public MainForm()
        {
            InitializeComponent();
            BuildUI();
            SelectType(HealthIdType.HPI_I);
            ShowTab(true);
        }

        // ════════════════════════════════════════════════════════════════════
        //  UI BUILD
        // ════════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            Text            = "Australian Health ID Generator & Validator";            Size            = new Size(980, 720);
            MinimumSize     = new Size(900, 640);
            BackColor       = ColBackground;
            Font            = FontBody;
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;

            // ── Header ──────────────────────────────────────────────────────
            var header = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 58,
                BackColor = ColPrimary,
                Padding   = new Padding(20, 0, 20, 0)
            };
            var lblSub = new Label
            {
                Text         = "HPI-I  ·  HPI-O  ·  IHI  ·  Medicare  ·  Provider No.",
                Font         = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor    = Color.FromArgb(190, 220, 255),
                AutoSize     = false,
                Dock         = DockStyle.Right,
                Width        = 350,
                Height       = 22,
                TextAlign    = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                Padding      = new Padding(0, 0, 4, 0)
            };
            var lblTitle = new Label
            {
                Text      = "Australian Health ID Generator & Validator",
                Font      = FontTitle,
                ForeColor = Color.White,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            // lblSub must be added FIRST so DockStyle.Right is reserved before Fill takes remaining space
            header.Controls.Add(lblSub);
            header.Controls.Add(lblTitle);

            // ── Tab strip ───────────────────────────────────────────────────
            var tabStrip = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 44,
                BackColor = Color.FromArgb(210, 218, 232)
            };

            _btnTabGen = MakeTabButton("⚙   Generator");
            _btnTabGen.Location = new Point(12, 4);
            _btnTabGen.Click   += (s, e) => ShowTab(true);

            _btnTabVal = MakeTabButton("✓   Validator");
            _btnTabVal.Location = new Point(164, 4);
            _btnTabVal.Click   += (s, e) => ShowTab(false);

            tabStrip.Controls.Add(_btnTabGen);
            tabStrip.Controls.Add(_btnTabVal);

            // ── Content ─────────────────────────────────────────────────────
            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 14) };
            _genPanel = BuildGeneratorPanel(); _genPanel.Dock = DockStyle.Fill;
            _valPanel = BuildValidatorPanel(); _valPanel.Dock = DockStyle.Fill;
            content.Controls.Add(_genPanel);
            content.Controls.Add(_valPanel);

            // ── Footer ──────────────────────────────────────────────────────
            var footer = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 26,
                BackColor = Color.FromArgb(222, 228, 240),
                Padding   = new Padding(16, 0, 16, 0)
            };
            footer.Controls.Add(new Label
            {
                Text      = "For testing purposes only  ·  Conforms to ADHA HI Service & Services Australia specifications",
                Font      = FontSmall,
                ForeColor = ColTextSecondary,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            });

            Controls.Add(content);
            Controls.Add(footer);
            Controls.Add(tabStrip);
            Controls.Add(header);
        }

        // ── Generator panel ────────────────────────────────────────────────

        private Panel BuildGeneratorPanel()
        {
            var root = new Panel { BackColor = Color.Transparent };

            // ── Left column ─────────────────────────────────────────────────
            int cardW = LeftWidth - 14;  // card width inside left panel

            var left = new Panel
            {
                Width     = LeftWidth,
                Dock      = DockStyle.Left,
                Padding   = new Padding(0, 0, 14, 0),
                BackColor = Color.Transparent
            };

            // Type selector card
            var typeCard = MakeCard("Identifier Type", cardW);
            typeCard.Location = new Point(0, 0);

            _pnlTypeHpii = MakeTypeRow("HPI-I",    "Healthcare Provider — Individual",   "800361xxxxxxxxx",  HealthIdType.HPI_I);
            _pnlTypeHpio = MakeTypeRow("HPI-O",    "Healthcare Provider — Organisation", "800362xxxxxxxxx",  HealthIdType.HPI_O);
            _pnlTypeIhi  = MakeTypeRow("IHI",      "Individual Healthcare Identifier",   "800360xxxxxxxxx",  HealthIdType.IHI);
            _pnlTypeMed  = MakeTypeRow("Medicare", "Medicare Card Number",               "X-XXX-XXXXX-Y/Z",  HealthIdType.Medicare);
            _pnlTypeProv = MakeTypeRow("Provider", "Medicare Provider Number",           "NNNNNN-LC",        HealthIdType.ProviderNumber);

            int ty = 30;
            foreach (var p in new[] { _pnlTypeHpii, _pnlTypeHpio, _pnlTypeIhi, _pnlTypeMed, _pnlTypeProv })
            {
                p.Location = new Point(8, ty);
                typeCard.Controls.Add(p);
                ty += p.Height;
            }
            typeCard.Height = ty + 10;

            // First row has no row above it to paint its top edge — add a top line explicitly
            _pnlTypeHpii.Paint += (s, e) =>
            {
                using var pen = new Pen(ColBorder, 1);
                e.Graphics.DrawLine(pen, 0, 0, _pnlTypeHpii.Width, 0);
            };

            // Provider options card
            _pnlProvOpts = MakeCard("Provider Options", cardW);
            _pnlProvOpts.Visible = false;

            _rdoRandom = new RadioButton
            {
                Text      = "Random — any structurally valid",
                Font      = FontBody,
                ForeColor = ColTextPrimary,
                AutoSize  = true,
                Location  = new Point(10, 32),
                Checked   = true
            };
            _rdoState = new RadioButton
            {
                Text      = "State-based numeric prefix",
                Font      = FontBody,
                ForeColor = ColTextPrimary,
                AutoSize  = true,
                Location  = new Point(10, 56)
            };
            _cmbState = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = FontBody,
                Width         = cardW - 24,
                Location      = new Point(10, 80),
                FlatStyle     = FlatStyle.Flat,
                Enabled       = false
            };
            foreach (var s in HealthIdEngine.GetProviderStateOptions())
                _cmbState.Items.Add(s);
            _cmbState.SelectedIndex = 0;
            _rdoRandom.CheckedChanged += (s, e) => _cmbState.Enabled = !_rdoRandom.Checked;
            _rdoState.CheckedChanged  += (s, e) => _cmbState.Enabled = _rdoState.Checked;
            _pnlProvOpts.Controls.AddRange(new Control[] { _rdoRandom, _rdoState, _cmbState });
            _pnlProvOpts.Height = 114;

            // Generate card
            var genCard = MakeCard("Generate", cardW);

            var lblCount = new Label
            {
                Text      = "Numbers to generate:",
                Font      = FontLabel,
                ForeColor = ColTextSecondary,
                AutoSize  = true,
                Location  = new Point(10, 34)
            };
            _nudCount = new NumericUpDown
            {
                Minimum     = 1,
                Maximum     = 500,
                Value       = 3,
                Font        = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor   = ColPrimary,
                Width       = 76,
                Height      = 36,
                Location    = new Point(10, 56),
                TextAlign   = HorizontalAlignment.Center,
                BorderStyle = BorderStyle.FixedSingle
            };
            var lblMax = new Label
            {
                Text      = "max 500",
                Font      = FontSmall,
                ForeColor = ColTextSecondary,
                AutoSize  = true,
                Location  = new Point(92, 66)
            };
            _btnGenerate = MakePrimaryButton("⚡  Generate", cardW - 20, 40);
            _btnGenerate.Location = new Point(10, 100);
            _btnGenerate.Click   += BtnGenerate_Click;

            genCard.Controls.AddRange(new Control[] { lblCount, _nudCount, lblMax, _btnGenerate });
            genCard.Height = 152;

            // Reposition cards when provider options toggle
            void ReflowLeft()
            {
                int y = typeCard.Bottom + 10;
                if (_pnlProvOpts.Visible) { _pnlProvOpts.Location = new Point(0, y); y = _pnlProvOpts.Bottom + 10; }
                genCard.Location = new Point(0, y);
            }
            _pnlProvOpts.VisibleChanged += (s, e) => ReflowLeft();
            ReflowLeft();

            left.Controls.Add(typeCard);
            left.Controls.Add(_pnlProvOpts);
            left.Controls.Add(genCard);

            // ── Right column: results ────────────────────────────────────────
            var right       = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var resultsCard = MakeCard("Generated Numbers", 0);
            resultsCard.Dock = DockStyle.Fill;

            // Toolbar
            var toolbar = new Panel
            {
                Height    = 38,
                BackColor = Color.Transparent,
                Location  = new Point(8, 30)
            };
            toolbar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _btnCopyAll   = MakeSecondaryButton("📋 Copy All",   112, 30);
            _btnCopyAll.Location = new Point(0, 4);
            _btnCopyAll.Click   += BtnCopyAll_Click;

            _btnExportCsv = MakeSecondaryButton("💾 Export CSV", 122, 30);
            _btnExportCsv.Location = new Point(118, 4);
            _btnExportCsv.Click   += BtnExportCsv_Click;

            _btnClearGen = MakeDangerButton("✕ Clear", 80, 30);
            _btnClearGen.Location = new Point(246, 4);
            _btnClearGen.Click   += (s, e) =>
            {
                _generatedNumbers.Clear();
                _copiedRowIndex = -1;
                _hoverRowIndex  = -1;
                RefreshResultsList();
                _lblGenStatus.Text      = "No numbers generated yet  ·  Click any row to copy";
                _lblGenStatus.ForeColor = ColTextSecondary;
            };
            toolbar.Controls.AddRange(new Control[] { _btnCopyAll, _btnExportCsv, _btnClearGen });

            _lstResults = new ListBox
            {
                Font                = FontMono,
                BackColor           = ColSurface,
                ForeColor           = ColTextPrimary,
                BorderStyle         = BorderStyle.None,
                SelectionMode       = SelectionMode.MultiExtended,
                HorizontalScrollbar = false,
                DrawMode            = DrawMode.OwnerDrawFixed,
                ItemHeight          = 30
            };
            _lstResults.Location      = new Point(8, 72);
            _lstResults.Anchor        = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _lstResults.DrawItem      += LstResults_DrawItem;
            _lstResults.MouseMove     += LstResults_MouseMove;
            _lstResults.MouseLeave    += LstResults_MouseLeave;
            _lstResults.MouseClick    += LstResults_MouseClick;
            _lstResults.MouseDoubleClick += (s, e) => CopyRow(_lstResults.IndexFromPoint(e.Location));

            _lblGenStatus = new Label
            {
                Text      = "No numbers generated yet  ·  Click any row to copy",
                Font      = FontSmall,
                ForeColor = ColTextSecondary,
                AutoSize  = false,
                Height    = 22,
                Dock      = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(4, 0, 0, 0)
            };

            resultsCard.Controls.Add(toolbar);
            resultsCard.Controls.Add(_lstResults);
            resultsCard.Controls.Add(_lblGenStatus);
            resultsCard.Resize += (s, e) =>
            {
                _lstResults.Size   = new Size(resultsCard.Width - 16, Math.Max(0, resultsCard.Height - 104));
                toolbar.Width      = resultsCard.Width - 16;
                _btnClearGen.Left  = toolbar.Width - _btnClearGen.Width;
            };

            right.Controls.Add(resultsCard);
            root.Controls.Add(right);
            root.Controls.Add(left);
            return root;
        }

        // ── Validator panel ────────────────────────────────────────────────

        private Panel BuildValidatorPanel()
        {
            var root = new Panel { BackColor = Color.Transparent };

            var inputCard = MakeCard("Enter Number to Validate", 0);
            inputCard.Height = 96;
            inputCard.Dock   = DockStyle.Top;

            const int InputY      = 34;   // y position for input row (below title + stripe)
            const int InputH      = 36;   // unified height for textbox AND buttons
            const int BtnValidW   = 110;
            const int BtnClearW   = 78;
            const int BtnGap      = 6;
            const int RightMargin = 12;

            // Wrap textbox in a panel so we can draw a persistent visible border
            var txtWrapper = new Panel
            {
                Height      = InputH,
                Location    = new Point(10, InputY),
                BackColor   = ColSurface,
                Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            txtWrapper.Paint += (s, e) =>
            {
                bool focused = _txtValidate.Focused;
                Color borderCol = focused ? ColPrimary : ColBorder;
                using var pen = new Pen(borderCol, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, txtWrapper.Width - 1, txtWrapper.Height - 1);
            };

            _txtValidate = new TextBox
            {
                Font        = FontMono,
                Height      = InputH - 4,
                Width       = 200,           // will be set in Resize
                Multiline   = true,
                Location    = new Point(2, 2),
                BorderStyle = BorderStyle.None,   // border drawn by wrapper panel
                PlaceholderText = "Paste HPI-I, HPI-O, IHI, Medicare or Provider Number…",
                Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor   = ColSurface
            };
            _txtValidate.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    RunValidation();
                }
            };
            // Repaint wrapper border on focus change so colour updates
            _txtValidate.GotFocus  += (s, e) => txtWrapper.Invalidate();
            _txtValidate.LostFocus += (s, e) => txtWrapper.Invalidate();

            txtWrapper.Controls.Add(_txtValidate);

            _btnValidate = MakePrimaryButton("Validate", BtnValidW, InputH);
            _btnValidate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnValidate.Click += (s, e) => RunValidation();

            _btnClearVal = MakeSecondaryButton("Clear", BtnClearW, InputH);
            _btnClearVal.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnClearVal.Click += (s, e) => { _txtValidate.Clear(); _pnlValidResult.Visible = false; };

            var lblHint = new Label
            {
                Text      = "Accepts formatted or unformatted input  ·  Auto-detects identifier type",
                Font      = FontSmall,
                ForeColor = ColTextSecondary,
                AutoSize  = true,
                Location  = new Point(10, InputY + InputH + 4)
            };

            inputCard.Controls.AddRange(new Control[] { txtWrapper, _btnValidate, _btnClearVal, lblHint });

            // Responsive layout: buttons pinned right, wrapper fills remaining width
            inputCard.Resize += (s, e) =>
            {
                int right              = inputCard.Width - RightMargin;
                _btnClearVal.Location  = new Point(right - BtnClearW, InputY);
                _btnValidate.Location  = new Point(_btnClearVal.Left - BtnGap - BtnValidW, InputY);
                int wrapW              = _btnValidate.Left - 10 - txtWrapper.Left;
                txtWrapper.Width       = Math.Max(10, wrapW);
                _txtValidate.Width     = Math.Max(4, txtWrapper.Width - 4);
            };

            _pnlValidResult = MakeCard("Validation Result", 0);
            _pnlValidResult.Dock    = DockStyle.Fill;
            _pnlValidResult.Visible = false;

            // Badge row
            var badgeRow = new Panel { Height = 54, BackColor = Color.Transparent, Location = new Point(10, 32) };
            badgeRow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _lblBadge = new Label
            {
                AutoSize  = false,
                Width     = 108,
                Height    = 36,
                Location  = new Point(0, 9),
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            RoundLabel(_lblBadge, 6);

            _lblValType = new Label
            {
                AutoSize  = false,
                Height    = 20,
                Location  = new Point(120, 10),
                Font      = FontHeading,
                ForeColor = ColTextPrimary,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _lblValDesc = new Label
            {
                AutoSize  = false,
                Height    = 18,
                Location  = new Point(120, 30),
                Font      = FontSmall,
                ForeColor = ColTextSecondary,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            badgeRow.Controls.AddRange(new Control[] { _lblBadge, _lblValType, _lblValDesc });
            badgeRow.Resize += (s, e) =>
            {
                _lblValType.Width = badgeRow.Width - 124;
                _lblValDesc.Width = badgeRow.Width - 124;
            };

            _pnlCheckRows = new Panel
            {
                AutoScroll = true,
                BackColor  = Color.Transparent,
                Location   = new Point(10, 90)
            };
            _pnlCheckRows.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            _pnlValidResult.Controls.Add(badgeRow);
            _pnlValidResult.Controls.Add(_pnlCheckRows);
            _pnlValidResult.Resize += (s, e) =>
            {
                badgeRow.Width     = _pnlValidResult.Width - 20;
                _pnlCheckRows.Size = new Size(_pnlValidResult.Width - 20, Math.Max(0, _pnlValidResult.Height - 98));
            };

            root.Controls.Add(_pnlValidResult);
            root.Controls.Add(inputCard);
            return root;
        }

        // ════════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ════════════════════════════════════════════════════════════════════

        private void BtnGenerate_Click(object? sender, EventArgs e)
        {
            int count = (int)_nudCount.Value;
            var mode  = ProviderNumberMode.Random;
            string? state = null;

            if (_selectedType == HealthIdType.ProviderNumber && _rdoState.Checked)
            {
                mode  = ProviderNumberMode.StateSpecific;
                state = _cmbState.SelectedIndex > 0 ? _cmbState.SelectedItem?.ToString() : null;
            }

            _generatedNumbers = HealthIdEngine.Generate(_selectedType, count, mode, state);
            _copiedRowIndex   = -1;
            _hoverRowIndex    = -1;
            RefreshResultsList();

            string typeLabel  = HealthIdEngine.GetTypeLabel(_selectedType);
            string stateNote  = mode == ProviderNumberMode.StateSpecific && state != null ? $" ({state})" : "";
            _lblGenStatus.Text      = $"Generated {count} {typeLabel}{stateNote}  ·  Click any row to copy";
            _lblGenStatus.ForeColor = ColTextSecondary;
        }

        private void BtnCopyAll_Click(object? sender, EventArgs e)
        {
            if (_generatedNumbers.Count == 0) return;
            Clipboard.SetText(string.Join(Environment.NewLine, _generatedNumbers));
            FlashStatus("All numbers copied to clipboard");
        }

        private void BtnExportCsv_Click(object? sender, EventArgs e)
        {
            if (_generatedNumbers.Count == 0) return;
            using var dlg = new SaveFileDialog
            {
                Title      = "Export generated numbers",
                Filter     = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt",
                FileName   = $"health_ids_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                DefaultExt = "csv"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var sb    = new StringBuilder();
            string tl = HealthIdEngine.GetTypeLabel(_selectedType);
            sb.AppendLine("Type,Number,Formatted");
            foreach (var n in _generatedNumbers)
                sb.AppendLine($"{tl},{n},{HealthIdEngine.FormatForDisplay(_selectedType, n)}");
            File.WriteAllText(dlg.FileName, sb.ToString());
            FlashStatus($"Exported {_generatedNumbers.Count} records");
        }

        private void RunValidation()
        {
            string input = _txtValidate.Text.Trim();
            if (!string.IsNullOrEmpty(input))
                ShowValidationResult(HealthIdEngine.Validate(input));
        }

        // ════════════════════════════════════════════════════════════════════
        //  RESULTS LIST — DRAW + COPY
        // ════════════════════════════════════════════════════════════════════

        private void LstResults_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _lstResults.Items.Count) return;

            bool selected = (e.State & DrawItemState.Selected) != 0;
            bool isHover  = e.Index == _hoverRowIndex && !selected;
            bool isCopied = e.Index == _copiedRowIndex;

            Color bg = selected ? ColPrimary
                     : isHover  ? ColRowHover
                     : e.Index % 2 == 0 ? ColSurface : ColRowAlt;

            e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);

            var sf = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Trimming      = StringTrimming.EllipsisCharacter,
                FormatFlags   = StringFormatFlags.NoWrap
            };

            // Row index
            Color idxCol = selected ? Color.FromArgb(160, 215, 255) : ColTextSecondary;
            var   idxR   = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, 30, e.Bounds.Height);
            e.Graphics.DrawString($"{e.Index + 1}.", FontSmall, new SolidBrush(idxCol), idxR, sf);

            // "✓ Copied!" flash — shown briefly after click
            const int CopiedW = 72;
            if (isCopied)
            {
                var copR = new Rectangle(e.Bounds.Right - CopiedW - 4, e.Bounds.Y + 5,
                                         CopiedW, e.Bounds.Height - 10);
                e.Graphics.FillRectangle(new SolidBrush(ColBadgeValid), copR);
                e.Graphics.DrawString("✓ Copied!", FontSmall, new SolidBrush(ColAccent), copR,
                    new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center });
            }

            // Number text — full width minus index, minus Copied area when flashing
            string numText  = _lstResults.Items[e.Index]?.ToString() ?? "";
            Color  numCol   = selected ? Color.White : ColTextPrimary;
            int    numRight = isCopied ? CopiedW + 8 : 4;
            var    numR     = new Rectangle(e.Bounds.X + 38, e.Bounds.Y,
                                            e.Bounds.Width - 38 - numRight, e.Bounds.Height);
            e.Graphics.DrawString(numText, FontMono, new SolidBrush(numCol), numR, sf);
        }

        private void LstResults_MouseMove(object? sender, MouseEventArgs e)
        {
            int idx = _lstResults.IndexFromPoint(e.Location);
            if (idx == _hoverRowIndex) return;
            int prev = _hoverRowIndex;
            _hoverRowIndex = idx;
            if (prev >= 0 && prev < _lstResults.Items.Count)
                _lstResults.Invalidate(_lstResults.GetItemRectangle(prev));
            if (idx >= 0 && idx < _lstResults.Items.Count)
                _lstResults.Invalidate(_lstResults.GetItemRectangle(idx));
        }

        private void LstResults_MouseLeave(object? sender, EventArgs e)
        {
            int prev = _hoverRowIndex;
            _hoverRowIndex = -1;
            if (prev >= 0 && prev < _lstResults.Items.Count)
                _lstResults.Invalidate(_lstResults.GetItemRectangle(prev));
        }

        private void LstResults_MouseClick(object? sender, MouseEventArgs e)
        {
            int idx = _lstResults.IndexFromPoint(e.Location);
            if (idx < 0 || idx >= _lstResults.Items.Count) return;
            CopyRow(idx);
        }

        private void CopyRow(int idx)
        {
            if (idx < 0 || idx >= _lstResults.Items.Count) return;
            string text = _lstResults.Items[idx]?.ToString() ?? "";
            if (string.IsNullOrEmpty(text)) return;
            Clipboard.SetText(text);

            _copyFlashTimer?.Stop();
            _copyFlashTimer?.Dispose();
            int prev = _copiedRowIndex;
            _copiedRowIndex = idx;
            if (prev >= 0 && prev < _lstResults.Items.Count)
                _lstResults.Invalidate(_lstResults.GetItemRectangle(prev));
            _lstResults.Invalidate(_lstResults.GetItemRectangle(idx));

            _copyFlashTimer = new System.Windows.Forms.Timer { Interval = 1400 };
            _copyFlashTimer.Tick += (s, e) =>
            {
                int old = _copiedRowIndex;
                _copiedRowIndex = -1;
                _copyFlashTimer?.Stop();
                _copyFlashTimer?.Dispose();
                _copyFlashTimer = null;
                if (old >= 0 && old < _lstResults.Items.Count)
                    _lstResults.Invalidate(_lstResults.GetItemRectangle(old));
            };
            _copyFlashTimer.Start();
        }

        private void RefreshResultsList()
        {
            _lstResults.Items.Clear();
            foreach (var n in _generatedNumbers)
                _lstResults.Items.Add(HealthIdEngine.FormatForDisplay(_selectedType, n));
        }

        // ════════════════════════════════════════════════════════════════════
        //  VALIDATION DISPLAY
        // ════════════════════════════════════════════════════════════════════

        private void ShowValidationResult(ValidationResult r)
        {
            _pnlValidResult.Visible = true;

            if (r.IsValid)
            {
                _lblBadge.Text      = "✓  VALID";
                _lblBadge.ForeColor = Color.FromArgb(0, 128, 75);
                _lblBadge.BackColor = ColBadgeValid;
            }
            else
            {
                _lblBadge.Text      = "✕  INVALID";
                _lblBadge.ForeColor = ColDanger;
                _lblBadge.BackColor = ColBadgeInvalid;
            }
            _lblBadge.Refresh();

            _lblValType.Text = r.TypeLabel;
            _lblValDesc.Text = r.FormatDescription;

            _pnlCheckRows.Controls.Clear();

            var rows = new List<(string Icon, string Label, string Value, bool? Pass)>
            {
                ("📄", "Input", r.InputValue, null)
            };

            if (!string.IsNullOrEmpty(r.LengthStatus))
                rows.Add((r.LengthStatus.StartsWith("PASS") ? "✓" : "✕",
                    "Length", r.LengthStatus,
                    r.LengthStatus.StartsWith("PASS")));

            if (!string.IsNullOrEmpty(r.PrefixStatus))
            {
                bool? pp = r.PrefixStatus.StartsWith("PASS") ? true
                         : r.PrefixStatus.StartsWith("INFO") ? (bool?)null : false;
                rows.Add((pp == true ? "✓" : pp == null ? "ℹ" : "✕",
                    "Prefix / Issuer", r.PrefixStatus, pp));
            }

            if (!string.IsNullOrEmpty(r.CheckDigitStatus))
            {
                bool? cp = r.CheckDigitStatus.StartsWith("PASS") ? true
                         : r.CheckDigitStatus.StartsWith("SKIP") ? (bool?)null : false;
                rows.Add((cp == true ? "✓" : cp == null ? "–" : "✕",
                    "Check digit/char", r.CheckDigitStatus, cp));
            }

            foreach (var d in r.Details)  rows.Add(("ℹ", "Detail", d, null));
            foreach (var err in r.Errors) rows.Add(("⚠", "Error",  err, false));

            int y = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = BuildCheckRow(rows[i].Icon, rows[i].Label, rows[i].Value, rows[i].Pass, i % 2 == 0);
                row.Location = new Point(0, y);
                row.Width    = _pnlCheckRows.Width;
                row.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                _pnlCheckRows.Controls.Add(row);
                y += row.Height;
            }
        }

        private static Panel BuildCheckRow(string icon, string label, string value, bool? pass, bool alt)
        {
            var row = new Panel { Height = 28, BackColor = alt ? ColRowAlt : ColSurface };

            Color iconCol = pass == null ? ColTextSecondary : pass.Value ? ColAccent : ColDanger;

            var lblIcon = new Label
            {
                Text      = icon,
                Font      = FontBody,
                ForeColor = iconCol,
                AutoSize  = false,
                Width     = 24, Height = 28,
                Location  = new Point(4, 0),
                TextAlign = ContentAlignment.MiddleCenter
            };
            var lblName = new Label
            {
                Text      = label + ":",
                Font      = FontLabel,
                ForeColor = ColTextSecondary,
                AutoSize  = false,
                Width     = 130, Height = 28,
                Location  = new Point(30, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var lblVal = new Label
            {
                Text      = value,
                Font      = FontMono,
                ForeColor = pass == false ? ColDanger : ColTextPrimary,
                AutoSize  = false,
                Height    = 28,
                Location  = new Point(164, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            row.Controls.AddRange(new Control[] { lblIcon, lblName, lblVal });
            row.Resize += (s, e) => lblVal.Width = Math.Max(10, row.Width - 168);
            return row;
        }

        // ════════════════════════════════════════════════════════════════════
        //  TYPE SELECTOR
        // ════════════════════════════════════════════════════════════════════

        private void SelectType(HealthIdType type)
        {
            _selectedType = type;

            var all = new (Panel Panel, HealthIdType T)[]
            {
                (_pnlTypeHpii, HealthIdType.HPI_I),
                (_pnlTypeHpio, HealthIdType.HPI_O),
                (_pnlTypeIhi,  HealthIdType.IHI),
                (_pnlTypeMed,  HealthIdType.Medicare),
                (_pnlTypeProv, HealthIdType.ProviderNumber),
            };

            foreach (var (panel, t) in all)
            {
                bool active = t == type;
                panel.BackColor = active ? Color.FromArgb(232, 243, 255) : ColSurface;
                foreach (Control c in panel.Controls)
                {
                    if (c.Tag is string tag)
                    {
                        if (tag == "accent") c.BackColor = active ? ColPrimary : ColBorder;
                        if (tag == "title")  c.ForeColor = active ? ColPrimary : ColTextPrimary;
                    }
                }
                panel.Invalidate(); // repaint separator line with new background
            }

            _pnlProvOpts.Visible = type == HealthIdType.ProviderNumber;

            if (_generatedNumbers.Count > 0)
            {
                _generatedNumbers.Clear();
                _copiedRowIndex = -1;
                _hoverRowIndex  = -1;
                RefreshResultsList();
                _lblGenStatus.Text      = "Switched type — click Generate";
                _lblGenStatus.ForeColor = ColTextSecondary;
            }
        }

        // Type selector row — single clean line: accent bar | bold label | description (truncated) | format right
        private Panel MakeTypeRow(string shortLabel, string description, string format, HealthIdType type)
        {
            // Row fills card inner width: cardW = LeftWidth-14, rows placed at x=8
            // Row right edge must reach cardW-1 (card right border), so RowW = cardW - 8 - 1 = LeftWidth - 23
            const int RowW     = LeftWidth - 23;
            const int RowH     = 44;
            const int AccentW  = 3;
            const int ShortW   = 72;
            const int FmtW     = 108;   // slightly narrower — creates gap with description
            const int Pad      = 8;
            const int FmtGap   = 10;    // explicit gap between description and format
            // Description occupies the remaining middle space
            int descX = AccentW + Pad + ShortW + 4;
            int descW = RowW - descX - FmtW - Pad - FmtGap;

            var row = new Panel
            {
                Width     = RowW,
                Height    = RowH,
                BackColor = ColSurface,
                Cursor    = Cursors.Hand
            };

            // Bottom separator only
            row.Paint += (s, e) =>
            {
                using var sep = new Pen(ColBorder, 1);
                e.Graphics.DrawLine(sep, 0, RowH - 1, row.Width, RowH - 1);
            };

            // Accent bar — fixed position, not DockStyle (avoids layout interference)
            var accent = new Panel
            {
                Width     = AccentW,
                Height    = RowH,
                Location  = new Point(0, 0),
                BackColor = ColBorder,
                Tag       = "accent"
            };

            // Short label — single line, vertically centred
            int shortTop = (RowH - 20) / 2;
            var lblShort = new Label
            {
                Text         = shortLabel,
                Font         = FontBold,
                ForeColor    = ColTextPrimary,
                AutoSize     = false,
                Width        = ShortW,
                Height       = 20,
                Location     = new Point(AccentW + Pad, shortTop),
                TextAlign    = ContentAlignment.MiddleLeft,
                Tag          = "title",
                AutoEllipsis = true,
                UseMnemonic  = false
            };

            // Description — single line, no wrapping possible (height = 1 line = 18px, offset to vertically centre)
            int descTop = (RowH - 18) / 2;
            var lblDesc = new Label
            {
                Text         = description,
                Font         = FontSmall,
                ForeColor    = ColTextSecondary,
                AutoSize     = false,
                Width        = descW,
                Height       = 18,
                Location     = new Point(descX, descTop),
                TextAlign    = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            // Format — right-pinned, single line, vertically centred
            int fmtTop = (RowH - 18) / 2;
            var lblFmt = new Label
            {
                Text      = format,
                Font      = new Font("Consolas", 8f),
                ForeColor = Color.FromArgb(140, 160, 190),
                AutoSize  = false,
                Width     = FmtW,
                Height    = 18,
                Location  = new Point(RowW - FmtW - Pad, fmtTop),
                TextAlign = ContentAlignment.MiddleRight
            };

            row.Controls.AddRange(new Control[] { accent, lblShort, lblDesc, lblFmt });

            var tip = new ToolTip();
            tip.SetToolTip(row,      $"{shortLabel} — {description}");
            tip.SetToolTip(lblShort, $"{shortLabel} — {description}");
            tip.SetToolTip(lblDesc,  description);

            EventHandler click = (s, e) => SelectType(type);
            row.Click += click;
            foreach (Control c in row.Controls) c.Click += click;
            return row;
        }

        // ════════════════════════════════════════════════════════════════════
        //  TAB SWITCHING
        // ════════════════════════════════════════════════════════════════════

        private void ShowTab(bool generator)
        {
            _genPanel.Visible = generator;
            _valPanel.Visible = !generator;

            // Active: white bg, primary blue text, bold
            _btnTabGen.BackColor = generator  ? ColSurface                        : Color.FromArgb(194, 204, 222);
            _btnTabGen.ForeColor = generator  ? ColPrimary                        : Color.FromArgb(70, 85, 110);
            _btnTabGen.Font      = generator  ? FontBold                          : FontBody;
            _btnTabGen.FlatAppearance.BorderColor = generator ? ColPrimary        : Color.FromArgb(170, 182, 202);

            _btnTabVal.BackColor = !generator ? ColSurface                        : Color.FromArgb(194, 204, 222);
            _btnTabVal.ForeColor = !generator ? ColPrimary                        : Color.FromArgb(70, 85, 110);
            _btnTabVal.Font      = !generator ? FontBold                          : FontBody;
            _btnTabVal.FlatAppearance.BorderColor = !generator ? ColPrimary       : Color.FromArgb(170, 182, 202);
        }

        private void FlashStatus(string msg)
        {
            _lblGenStatus.Text      = msg;
            _lblGenStatus.ForeColor = ColAccent;
            var t = new System.Windows.Forms.Timer { Interval = 2400 };
            t.Tick += (s, e) =>
            {
                _lblGenStatus.ForeColor = ColTextSecondary;
                _lblGenStatus.Text = _generatedNumbers.Count > 0
                    ? $"{_generatedNumbers.Count} numbers  ·  Click any row to copy"
                    : "No numbers generated yet  ·  Click any row to copy";
                t.Stop();
            };
            t.Start();
        }

        // ════════════════════════════════════════════════════════════════════
        //  CONTROL FACTORIES
        // ════════════════════════════════════════════════════════════════════

        private static Panel MakeCard(string title, int width)
        {
            var card = new Panel { BackColor = ColSurface };
            if (width > 0) card.Width = width;

            // Border + blue top stripe painted together so they don't fight
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                // Outer border
                using var borderPen = new Pen(ColBorder, 1);
                g.DrawRectangle(borderPen, 0, 0, card.Width - 1, card.Height - 1);
                // Blue accent stripe inside the border (y=1 so it doesn't overdraw the border)
                g.FillRectangle(new SolidBrush(ColPrimary), 1, 1, card.Width - 2, 3);
            };

            card.Controls.Add(new Label
            {
                Text      = title,
                Font      = FontHeading,
                ForeColor = ColTextSecondary,
                AutoSize  = false,
                Height    = 26,
                Dock      = DockStyle.Top,
                Padding   = new Padding(8, 4, 0, 0)
            });
            return card;
        }

        private static Button MakePrimaryButton(string text, int width, int height)
        {
            var btn = new Button
            {
                Text      = text,
                Width     = width,
                Height    = height,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColPrimary,
                ForeColor = Color.White,
                Font      = FontBold,
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = ColPrimaryHover;
            btn.MouseLeave += (s, e) => btn.BackColor = ColPrimary;
            return btn;
        }

        private static Button MakeSecondaryButton(string text, int width, int height)
        {
            var btn = new Button
            {
                Text      = text,
                Width     = width,
                Height    = height,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColSurface,
                ForeColor = ColPrimary,
                Font      = FontBody,
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = ColBorder;
            btn.FlatAppearance.BorderSize  = 1;
            btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(235, 243, 255);
            btn.MouseLeave += (s, e) => btn.BackColor = ColSurface;
            return btn;
        }

        private static Button MakeDangerButton(string text, int width, int height)
        {
            var btn = new Button
            {
                Text      = text,
                Width     = width,
                Height    = height,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColSurface,
                ForeColor = ColDanger,
                Font      = FontBody,
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(240, 200, 200);
            btn.FlatAppearance.BorderSize  = 1;
            btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(255, 240, 240);
            btn.MouseLeave += (s, e) => btn.BackColor = ColSurface;
            return btn;
        }

        private static Button MakeTabButton(string text)
        {
            var btn = new Button
            {
                Text      = text,
                Width     = 144,
                Height    = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(194, 204, 222),  // visible inactive bg
                ForeColor = Color.FromArgb(70, 85, 110),    // legible inactive text
                Font      = FontBody,
                Cursor    = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding   = new Padding(0)
            };
            btn.FlatAppearance.BorderColor         = Color.FromArgb(170, 182, 202);
            btn.FlatAppearance.BorderSize          = 1;
            btn.FlatAppearance.MouseOverBackColor  = Color.FromArgb(220, 228, 242);
            return btn;
        }

        private static void DrawBorder(Control ctrl, Color color, int thickness)
        {
            ctrl.Paint += (s, e) =>
            {
                using var pen = new Pen(color, thickness);
                e.Graphics.DrawRectangle(pen, 0, 0, ctrl.Width - 1, ctrl.Height - 1);
            };
        }

        private static void RoundLabel(Label lbl, int radius)
        {
            lbl.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, lbl.Width - 1, lbl.Height - 1), radius);
                e.Graphics.FillPath(new SolidBrush(lbl.BackColor), path);
                using var sf = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(lbl.Text, lbl.Font, new SolidBrush(lbl.ForeColor),
                    new RectangleF(0, 0, lbl.Width, lbl.Height), sf);
            };
        }

        private static GraphicsPath RoundedRect(Rectangle b, int r)
        {
            var p = new GraphicsPath();
            p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90);
            p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90);
            p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90);
            p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
