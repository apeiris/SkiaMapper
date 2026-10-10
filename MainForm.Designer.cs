namespace SkiaMapper {
    partial class MainForm {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent() {
            tcMain = new TabControl();
            tbpMapper = new TabPage();
            mapperControl = new SkiaMapper.Controls.SkiaMapperControl();
            tbpRepository = new TabPage();
            pnlRepo = new TableLayoutPanel();
            lblSource = new Label();
            txtRepositorySource = new TextBox();
            btnBrowseSource = new Button();
            lblDest = new Label();
            txtRepositoryDestination = new TextBox();
            btnBrowseDestination = new Button();
            lblXsltFile = new Label();
            txtXsltFile = new TextBox();
            btnBrowseXslt = new Button();
            tcMain.SuspendLayout();
            tbpMapper.SuspendLayout();
            tbpRepository.SuspendLayout();
            pnlRepo.SuspendLayout();
            SuspendLayout();
            // 
            // tcMain
            // 
            tcMain.Controls.Add(tbpMapper);
            tcMain.Controls.Add(tbpRepository);
            tcMain.Dock = DockStyle.Fill;
            tcMain.Location = new Point(0, 0);
            tcMain.Name = "tcMain";
            tcMain.SelectedIndex = 0;
            tcMain.Size = new Size(1100, 750);
            tcMain.TabIndex = 0;
            // 
            // tbpMapper
            // 
            tbpMapper.Controls.Add(mapperControl);
            tbpMapper.Location = new Point(4, 24);
            tbpMapper.Name = "tbpMapper";
            tbpMapper.Padding = new Padding(3);
            tbpMapper.Size = new Size(1092, 722);
            tbpMapper.TabIndex = 0;
            tbpMapper.Text = "Data Mapper Workspace";
            tbpMapper.UseVisualStyleBackColor = true;
            // 
            // mapperControl
            // 
            mapperControl.Dock = DockStyle.Fill;
            mapperControl.Location = new Point(3, 3);
            mapperControl.Name = "mapperControl";
            mapperControl.Size = new Size(1086, 716);
            mapperControl.TabIndex = 0;
            // 
            // tbpRepository
            // 
            tbpRepository.Controls.Add(pnlRepo);
            tbpRepository.Location = new Point(4, 24);
            tbpRepository.Name = "tbpRepository";
            tbpRepository.Padding = new Padding(3);
            tbpRepository.Size = new Size(1092, 722);
            tbpRepository.TabIndex = 1;
            tbpRepository.Text = "Repository";
            tbpRepository.UseVisualStyleBackColor = true;
            // 
            // pnlRepo
            // 
            pnlRepo.AutoSize = true;
            pnlRepo.ColumnCount = 3;
            pnlRepo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            pnlRepo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlRepo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F));
            pnlRepo.Controls.Add(lblSource, 0, 0);
            pnlRepo.Controls.Add(txtRepositorySource, 1, 0);
            pnlRepo.Controls.Add(btnBrowseSource, 2, 0);
            pnlRepo.Controls.Add(lblDest, 0, 1);
            pnlRepo.Controls.Add(txtRepositoryDestination, 1, 1);
            pnlRepo.Controls.Add(btnBrowseDestination, 2, 1);
            pnlRepo.Controls.Add(lblXsltFile, 0, 2);
            pnlRepo.Controls.Add(txtXsltFile, 1, 2);
            pnlRepo.Controls.Add(btnBrowseXslt, 2, 2);
            pnlRepo.Dock = DockStyle.Top;
            pnlRepo.Location = new Point(3, 3);
            pnlRepo.Name = "pnlRepo";
            pnlRepo.Padding = new Padding(20);
            pnlRepo.RowCount = 3;
            pnlRepo.RowStyles.Add(new RowStyle());
            pnlRepo.RowStyles.Add(new RowStyle());
            pnlRepo.RowStyles.Add(new RowStyle());
            pnlRepo.Size = new Size(1086, 127);
            pnlRepo.TabIndex = 0;
            // 
            // lblSource
            // 
            lblSource.Anchor = AnchorStyles.Left;
            lblSource.AutoSize = true;
            lblSource.Location = new Point(23, 27);
            lblSource.Name = "lblSource";
            lblSource.Size = new Size(73, 15);
            lblSource.TabIndex = 0;
            lblSource.Text = "Source Path:";
            // 
            // txtRepositorySource
            // 
            txtRepositorySource.Dock = DockStyle.Fill;
            txtRepositorySource.Location = new Point(173, 23);
            txtRepositorySource.Name = "txtRepositorySource";
            txtRepositorySource.Size = new Size(840, 23);
            txtRepositorySource.TabIndex = 1;
            // 
            // btnBrowseSource
            // 
            btnBrowseSource.Dock = DockStyle.Fill;
            btnBrowseSource.Location = new Point(1019, 23);
            btnBrowseSource.Name = "btnBrowseSource";
            btnBrowseSource.Size = new Size(44, 23);
            btnBrowseSource.TabIndex = 2;
            btnBrowseSource.Text = "...";
            // 
            // lblDest
            // 
            lblDest.Anchor = AnchorStyles.Left;
            lblDest.AutoSize = true;
            lblDest.Location = new Point(23, 56);
            lblDest.Name = "lblDest";
            lblDest.Size = new Size(97, 15);
            lblDest.TabIndex = 3;
            lblDest.Text = "Destination Path:";
            // 
            // txtRepositoryDestination
            // 
            txtRepositoryDestination.Dock = DockStyle.Fill;
            txtRepositoryDestination.Location = new Point(173, 52);
            txtRepositoryDestination.Name = "txtRepositoryDestination";
            txtRepositoryDestination.Size = new Size(840, 23);
            txtRepositoryDestination.TabIndex = 4;
            // 
            // btnBrowseDestination
            // 
            btnBrowseDestination.Dock = DockStyle.Fill;
            btnBrowseDestination.Location = new Point(1019, 52);
            btnBrowseDestination.Name = "btnBrowseDestination";
            btnBrowseDestination.Size = new Size(44, 23);
            btnBrowseDestination.TabIndex = 5;
            btnBrowseDestination.Text = "...";
            // 
            // lblXsltFile
            // 
            lblXsltFile.Anchor = AnchorStyles.Left;
            lblXsltFile.AutoSize = true;
            lblXsltFile.Location = new Point(23, 85);
            lblXsltFile.Name = "lblXsltFile";
            lblXsltFile.Size = new Size(61, 15);
            lblXsltFile.TabIndex = 6;
            lblXsltFile.Text = "XSLT Path:";
            // 
            // txtXsltFile
            // 
            txtXsltFile.Dock = DockStyle.Fill;
            txtXsltFile.Location = new Point(173, 81);
            txtXsltFile.Name = "txtXsltFile";
            txtXsltFile.Size = new Size(840, 23);
            txtXsltFile.TabIndex = 7;
            // 
            // btnBrowseXslt
            // 
            btnBrowseXslt.Dock = DockStyle.Fill;
            btnBrowseXslt.Location = new Point(1019, 81);
            btnBrowseXslt.Name = "btnBrowseXslt";
            btnBrowseXslt.Size = new Size(44, 23);
            btnBrowseXslt.TabIndex = 8;
            btnBrowseXslt.Text = "...";
            // 
            // MainForm
            // 
            ClientSize = new Size(1100, 750);
            Controls.Add(tcMain);
            Name = "MainForm";
            Text = "SkiaMapper - C# Enterprise Pipeline Blueprint";
            tcMain.ResumeLayout(false);
            tbpMapper.ResumeLayout(false);
            tbpRepository.ResumeLayout(false);
            tbpRepository.PerformLayout();
            pnlRepo.ResumeLayout(false);
            pnlRepo.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TabControl tcMain;
        private System.Windows.Forms.TabPage tbpMapper;
        private System.Windows.Forms.TabPage tbpRepository;
        private System.Windows.Forms.TableLayoutPanel pnlRepo;
        private System.Windows.Forms.Label lblSource;
        private System.Windows.Forms.TextBox txtRepositorySource;
        private System.Windows.Forms.Button btnBrowseSource;
        private System.Windows.Forms.Label lblDest;
        private System.Windows.Forms.TextBox txtRepositoryDestination;
        private System.Windows.Forms.Button btnBrowseDestination;
        private System.Windows.Forms.Label lblXsltFile;
        public System.Windows.Forms.TextBox txtXsltFile;
        private System.Windows.Forms.Button btnBrowseXslt;
        private SkiaMapper.Controls.SkiaMapperControl mapperControl;
    }
}