using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using HIDLibraryNS;
using System.Threading;
using System.Runtime.InteropServices;
//using Microsoft.Office.Interop.Excel;


namespace ConfigAppNS
{
    public partial class App_Main : Form
    {
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        /// =============================================================================================
        /// ====  App Main Init  ====
        /// =============================================================================================
        ///
        public App_Main()
        {
            ///------------------------------------------------------------------------------------------
            InitializeComponent();


            //SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            //pictureBox_Test2.BackColor = Color.Transparent;
            //pictureBox_Test2.BackgroundImage = System.Drawing.Image.FromFile(file.FullName);
            //pictureBox_Test2.BackgroundImageLayout = ImageLayout.None;
            //pictureBox_Test2.Image = Image.FromFile(file.FullName);

            //pictureBox_Test1.Parent = this.panel1;
            //pictureBox_Test2.Parent = this.panel1;
            //pictureBox_Test3.Parent = this.panel1;

            //pictureBox_Test1.Visible = false;
            //pictureBox_Test2.Visible = false;
            //pictureBox_Test3.Visible = false;

            //pictureBox_Test.Parent = this.panel1;
            //pictureBox_Test2.Image = System.Drawing.Image.FromFile(file.FullName);
            //pictureBox_Test2.BackgroundImageLayout = ImageLayout.Zoom;
            //pictureBox_Test2.Visible = true;


            ///------------------------------------------------------------------------------------------
            ReadAppConfig();
            ///------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ====  Read Config Init  ====
        /// =============================================================================================
        ///
        private void ReadAppConfig()
        {
            //----------------------------------------------------------------------------------------------
            string Currentpath = Directory.GetCurrentDirectory() + "\\Config.ini";
            string Str = "";
            //----------------------------------------------------------------------------------------------
            string AppName = "ConfigApp";
            //----------------------------------------------------------------------------------------------
            using (StreamReader Rhandle = File.OpenText(Currentpath))         // Create a file to write to.
            {
                for (byte i = 0; i < 5; i++)
                {
                    Str = Rhandle.ReadLine();
                    if (Str == "[APP Config]")
                    {
                        for (byte j = 0; j < 50; j++)
                        {
                            Str = Rhandle.ReadLine();
                            string[] S = Str.Split(';');
                            Str = S[0];
                            string[] StrTab = Str.Split('=');
                            Str = StrTab[0];
                            if (Str == "")
                            {
                                break;
                            }
                            else if (Str == "NAME")
                            {
                                AppName = StrTab[1];
                            }
                            else if (Str == "CONFIGEND")
                            {
                                if (StrTab[1] == "YES") break;
                            }
                        }
                    }
                }
            }
            //----------------------------------------------------------------------------------------------
            this.Text = AppName;
            //----------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ====  Btn ADX Click  ====
        /// =============================================================================================
        ///
        private void Btn_ADX_Click(object sender, EventArgs e)
        {
            App_ADX adx = new App_ADX();
            //this.Hide();
            adx.Show();
        }

        /// =============================================================================================
        /// ====  Btn DDR Click  ====
        /// =============================================================================================
        ///
        private void Btn_DDR_Click(object sender, EventArgs e)
        {
            ///------------------------------------------------------------------------------------------
            App_DDR ddr = new App_DDR();
            //this.Hide();
            ddr.Show();
            //this.Close();
            ///------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ====  Btn ZER Click  ====
        /// =============================================================================================
        ///
        private void Btn_ZER_Click(object sender, EventArgs e)
        {
            ///------------------------------------------------------------------------------------------
            App_ZER zer = new App_ZER();
            //this.Hide();
            zer.Show();
            //this.Close();
            ///------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ====  Btn OTA Click  ====
        /// =============================================================================================
        ///
        private void Btn_OTA_Click(object sender, EventArgs e)
        {
            ///------------------------------------------------------------------------------------------
            //App_OTA ota = new App_OTA();
            //this.Hide();
            //ota.Show();
            ///------------------------------------------------------------------------------------------
        }

        private void Btn_YJI_Click(object sender, EventArgs e)
        {
            ///------------------------------------------------------------------------------------------
            App_YJI Yji = new App_YJI();
            //this.Hide();
            Yji.Show();
            //this.Close();
            ///------------------------------------------------------------------------------------------
        }



        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
    }
}
