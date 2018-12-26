using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyVideoPlayer
{
    class TransparentPanel : Control
    {
        public TransparentPanel() { }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            //不進行背景繪製
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000020; //WS_EX_TRANSPARENT
                return cp;
            }
        }

        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            //繪製panel背影圖像
            if (BackgroundImage != null) e.Graphics.DrawImage(this.BackgroundImage, new Point(0, 0));
        }
    }
}
