﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace Neo.UI.DbcEditors.Exemple
{
    [Designer(typeof(ExempleEditorControlDesigner))]
    public partial class ExempleEditorControl : UserControl
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public TabControl TabControl
        {
            get { return this.tbcEditor; }
        }

        public ExempleEditorControl()
        {
            InitializeComponent();
	        this.tbcEditor.Appearance = TabAppearance.FlatButtons;
	        this.tbcEditor.ItemSize = new Size(0, 1);
	        this.tbcEditor.SizeMode = TabSizeMode.Fixed;
        }
    }

    internal class ExempleEditorControlDesigner : ControlDesigner
    {
        public override void Initialize(IComponent component)
        {
            base.Initialize(component);

            var ctl = (this.Control as ExempleEditorControl).TabControl as TabControl;
            EnableDesignMode(ctl, "TabControl");
            foreach (TabPage page in ctl.TabPages)
            {
	            EnableDesignMode(page, page.Name);
            }
        }
    }
}