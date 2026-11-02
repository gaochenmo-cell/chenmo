namespace ConfigAppNS
{
    partial class AppMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.Btn_ADX = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // Btn_ADX
            // 
            this.Btn_ADX.Location = new System.Drawing.Point(114, 81);
            this.Btn_ADX.Name = "Btn_ADX";
            this.Btn_ADX.Size = new System.Drawing.Size(102, 47);
            this.Btn_ADX.TabIndex = 0;
            this.Btn_ADX.Text = "ADX";
            this.Btn_ADX.UseVisualStyleBackColor = true;
            // 
            // AppMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(624, 386);
            this.Controls.Add(this.Btn_ADX);
            this.Name = "AppMain";
            this.Text = "Form1";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button Btn_ADX;
    }
}