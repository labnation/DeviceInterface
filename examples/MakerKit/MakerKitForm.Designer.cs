using System.Collections.Generic;

namespace MakerKit
{
    partial class MakerKitForm
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
        private void InitializeComponent(List<RegisterBankDefinition> registerDefinitions)
        {
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.btnUpload = new System.Windows.Forms.Button();
            this.lblUpload = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            statusLabel = new System.Windows.Forms.Label();

            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.Location = new System.Drawing.Point(12, 20);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(260, 68);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // btnUpload
            // 
            this.btnUpload.Location = new System.Drawing.Point(12, 340);
            this.btnUpload.Name = "btnUpload";
            this.btnUpload.Size = new System.Drawing.Size(260, 29);
            this.btnUpload.TabIndex = 1;
            this.btnUpload.Text = "Upload bitstream";
            this.btnUpload.UseVisualStyleBackColor = true;
            // 
            // lblUpload
            // 
            this.lblUpload.AutoSize = true;
            this.lblUpload.Location = new System.Drawing.Point(144, 95);
            this.lblUpload.Name = "lblUpload";
            this.lblUpload.Size = new System.Drawing.Size(0, 13);
            this.lblUpload.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Arial", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(42, 86);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(199, 22);
            this.label1.TabIndex = 3;
            this.label1.Text = "SmartScope Maker Kit";
            
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(284, 370);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lblUpload);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(statusLabel);
            this.Name = "Form1";
            this.Text = "SmartScope Maker Kit";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

            int startY = 148;
            int offset = 25;
            for (int i = 0; i < registerDefinitions[0].Registers.Length; i++)
            {
                System.Windows.Forms.Label label = new System.Windows.Forms.Label();
                label.AutoSize = true;
                label.Location = new System.Drawing.Point(12, startY + i * offset);
                label.Name = "lblReg"+i.ToString();
                label.Size = new System.Drawing.Size(33, 13);
                label.TabIndex = 3;
                label.Text = i.ToString("000") + ": " + registerDefinitions[0].Registers[i];
                this.Controls.Add(label);

                System.Windows.Forms.TextBox textbox = new System.Windows.Forms.TextBox();
                textbox.Location = new System.Drawing.Point(172, startY + 1 + i * offset);
                textbox.Name = "txtReg".ToString();
                textbox.Size = new System.Drawing.Size(100, 20);
                textbox.TabIndex = 4;
                textbox.Text = "0";
                textbox.TextChanged += textbox_TextChanged;
                textbox.Tag = i;
                this.Controls.Add(textbox);
            }

            // 
            // statusLabel
            // 
            statusLabel.AutoSize = true;
            statusLabel.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            statusLabel.Location = new System.Drawing.Point(20, startY + offset * registerDefinitions[0].Registers.Length + 10);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new System.Drawing.Size(199, 22);
            statusLabel.TabIndex = 3;
            statusLabel.Text = "Form initialized";
        }        

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Button btnUpload;
        private System.Windows.Forms.Label lblUpload;
        private System.Windows.Forms.Label label1;
        public static System.Windows.Forms.Label statusLabel;
    }
}

