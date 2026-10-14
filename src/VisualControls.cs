using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

class RoundButton : Control, IButtonControl {
    bool hover,pressed;
    public DialogResult DialogResult { get; set; }
    public RoundButton() {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable|ControlStyles.StandardClick,true);
        BackColor=Color.FromArgb(39,43,53); ForeColor=Color.FromArgb(232,234,240); Cursor=Cursors.Default; Size=new Size(100,34); Margin=new Padding(0,0,8,0); TabStop=true; AccessibleRole=AccessibleRole.PushButton;
    }
    public void NotifyDefault(bool value) { Invalidate(); }
    public void PerformClick() { if(CanSelect) OnClick(EventArgs.Empty); }
    protected override void OnClick(EventArgs e) { if(!Enabled) return; base.OnClick(e); if(DialogResult!=DialogResult.None && FindForm()!=null) FindForm().DialogResult=DialogResult; }
    protected override void OnMouseEnter(EventArgs e) { hover=true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover=false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if(e.Button==MouseButtons.Left) { pressed=true; Focus(); Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed=false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseCaptureChanged(EventArgs e) { pressed=false; Invalidate(); base.OnMouseCaptureChanged(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if(e.KeyCode==Keys.Space) { pressed=true; Invalidate(); e.Handled=true; } base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { if(e.KeyCode==Keys.Space && pressed) { pressed=false; Invalidate(); PerformClick(); e.Handled=true; } base.OnKeyUp(e); }
    protected override bool ProcessDialogKey(Keys keys) { if(keys==Keys.Enter) { PerformClick(); return true; } return base.ProcessDialogKey(keys); }
    protected override void OnLostFocus(EventArgs e) { pressed=false; Invalidate(); base.OnLostFocus(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { pressed=false; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaintBackground(PaintEventArgs e) { using(var brush=new SolidBrush(Parent==null?Color.FromArgb(19,21,27):Parent.BackColor)) e.Graphics.FillRectangle(brush,ClientRectangle); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        Color fill=Enabled && pressed?ControlPaint.Dark(BackColor,0.12f):Enabled && hover?ControlPaint.Light(BackColor,0.15f):BackColor;
        using(var path=RoundedGeometry.Path(new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1)),10)) using(var brush=new SolidBrush(fill)) e.Graphics.FillPath(brush,path);
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:Color.FromArgb(103,109,123),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
        if(Focused && ShowFocusCues && Width>12 && Height>12) ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(6,6,Width-12,Height-12),ForeColor,fill);
    }
}

// The list is a child of the main form: it has no top-level popup or DWM shadow.
class DarkComboBox : UserControl, IMessageFilter {
    public sealed class ItemCollection : Collection<object> {
        readonly DarkComboBox owner;
        internal ItemCollection(DarkComboBox owner) { this.owner=owner; }
        public void AddRange(object[] values) { foreach(object value in values) Add(value); }
        protected override void ClearItems() { owner.CloseList(false); base.ClearItems(); owner.SelectedIndex=-1; }
        protected override void InsertItem(int index,object value) { owner.CloseList(false); base.InsertItem(index,value); }
        protected override void RemoveItem(int index) { owner.CloseList(false); base.RemoveItem(index); owner.SelectedIndex=-1; }
        protected override void SetItem(int index,object value) { owner.CloseList(false); base.SetItem(index,value); owner.SelectedIndex=-1; }
    }
    sealed class ChoiceList : ListBox {
        readonly DarkComboBox owner;
        internal ChoiceList(DarkComboBox owner) { this.owner=owner; }
        protected override bool ProcessCmdKey(ref Message message,Keys keys) {
            if(keys==Keys.Escape) { owner.CloseList(true); return true; }
            if(keys==Keys.Enter) { owner.Commit(); return true; }
            if(keys==Keys.Tab || keys==(Keys.Shift|Keys.Tab)) { owner.CloseList(true); owner.FindForm().SelectNextControl(owner,keys==Keys.Tab,true,true,true); return true; }
            return base.ProcessCmdKey(ref message,keys);
        }
        protected override void WndProc(ref Message m) { if(m.Msg==0x20) { Cursor.Current=Cursors.Default; m.Result=new IntPtr(1); return; } base.WndProc(ref m); }
    }
    TextBox editor; int selected=-1; ComboBoxStyle style; bool updating;
    RoundPanel overlay; ChoiceList list; Form ownerForm;
    public ItemCollection Items { get; private set; }
    public event EventHandler SelectedIndexChanged;
    public ComboBoxStyle DropDownStyle { get { return style; } set { style=value; if(editor!=null) { editor.Visible=value!=ComboBoxStyle.DropDownList; TabStop=!editor.Visible; } Invalidate(); } }
    public int SelectedIndex {
        get { return selected; }
        set {
            if(value < -1 || value>=Items.Count) throw new ArgumentOutOfRangeException("value");
            if(selected==value) return; selected=value; Text=value<0?"":Convert.ToString(Items[value]);
            if(SelectedIndexChanged!=null) SelectedIndexChanged(this,EventArgs.Empty);
        }
    }
    public object SelectedItem { get { return selected<0?null:Items[selected]; } set { SelectedIndex=Items.IndexOf(value); } }
    public override string Text {
        get { return base.Text; }
        set { value=value??""; if(editor!=null && editor.Text!=value) { updating=true; editor.Text=value; updating=false; } base.Text=value; Invalidate(); }
    }
    public bool DroppedDown { get { return overlay!=null; } set { if(value) OpenList(); else CloseList(false); } }
    public IntPtr PopupHandle { get { return overlay==null?IntPtr.Zero:overlay.Handle; } }
    public DarkComboBox() {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        Items=new ItemCollection(this); BackColor=Color.FromArgb(39,43,53); ForeColor=Color.FromArgb(232,234,240); Size=new Size(180,28); Cursor=Cursors.Default;
        editor=new TextBox { BorderStyle=BorderStyle.None,BackColor=BackColor,ForeColor=ForeColor,Cursor=Cursors.Default };
        editor.TextChanged+=delegate { if(updating) return; selected=-1; base.Text=editor.Text; };
        Controls.Add(editor); DropDownStyle=ComboBoxStyle.DropDown; LayoutEditor(); AccessibleRole=AccessibleRole.ComboBox;
    }
    void LayoutEditor() { if(editor!=null) editor.SetBounds(9,Math.Max(3,(Height-editor.PreferredHeight)/2),Math.Max(1,Width-35),editor.PreferredHeight); }
    protected override void OnResize(EventArgs e) { CloseList(false); base.OnResize(e); LayoutEditor(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if(editor!=null) editor.Font=Font; LayoutEditor(); }
    protected override void OnBackColorChanged(EventArgs e) { base.OnBackColorChanged(e); if(editor!=null) editor.BackColor=BackColor; }
    protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); if(editor!=null) editor.ForeColor=ForeColor; }
    protected override void OnVisibleChanged(EventArgs e) { if(!Visible) CloseList(false); base.OnVisibleChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { if(!Enabled) CloseList(false); base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { if(e.Button==MouseButtons.Left) DroppedDown=!DroppedDown; base.OnMouseDown(e); }
    protected override bool ProcessCmdKey(ref Message message,Keys keys) {
        if(keys==Keys.F4 || keys==(Keys.Alt|Keys.Down)) { DroppedDown=!DroppedDown; return true; }
        if(style==ComboBoxStyle.DropDownList && (keys==Keys.Down || keys==Keys.Up)) {
            if(Items.Count>0) SelectedIndex=Math.Max(0,Math.Min(Items.Count-1,selected+(keys==Keys.Down?1:-1))); return true;
        }
        return base.ProcessCmdKey(ref message,keys);
    }
    protected override void OnPaintBackground(PaintEventArgs e) { using(var brush=new SolidBrush(Parent==null?Color.FromArgb(29,32,40):Parent.BackColor)) e.Graphics.FillRectangle(brush,ClientRectangle); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=RoundedGeometry.Path(new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1)),8)) using(var brush=new SolidBrush(BackColor)) e.Graphics.FillPath(brush,path);
        if(style==ComboBoxStyle.DropDownList) TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(9,0,Math.Max(1,Width-35),Height),Enabled?ForeColor:SystemColors.GrayText,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        int x=Width-12,y=Height/2; using(var pen=new Pen(ForeColor,1.3f)) e.Graphics.DrawLines(pen,new[]{new Point(x-3,y-2),new Point(x,y+1),new Point(x+3,y-2)});
    }
    void OpenList() {
        if(DroppedDown || !Enabled || !Visible || Items.Count==0) return;
        ownerForm=FindForm(); if(ownerForm==null) return;
        var bounds=ownerForm.RectangleToClient(RectangleToScreen(ClientRectangle)); int itemHeight=Math.Max(26,Font.Height+10);
        int below=ownerForm.ClientSize.Height-bounds.Bottom-12,above=bounds.Top-12;
        bool aboveList=below<itemHeight*3 && above>below; int height=Math.Min(itemHeight*Math.Min(8,Items.Count)+12,Math.Max(itemHeight+12,aboveList?above:below));
        int width=Math.Min(Math.Max(Width,150),ownerForm.ClientSize.Width-24),x=Math.Max(12,Math.Min(bounds.Left,ownerForm.ClientSize.Width-width-12));
        overlay=new RoundPanel { BackColor=Color.FromArgb(39,43,53),Bounds=new Rectangle(x,aboveList?Math.Max(12,bounds.Top-height):bounds.Bottom,width,height),Padding=new Padding(6),Margin=new Padding(0),Font=Font };
        list=new ChoiceList(this) { Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,IntegralHeight=false,BackColor=overlay.BackColor,ForeColor=ForeColor,Font=Font,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=itemHeight,Cursor=Cursors.Default };
        foreach(object item in Items) list.Items.Add(item); list.SelectedIndex=selected;
        list.DrawItem+=delegate(object sender,DrawItemEventArgs e) { if(e.Index<0) return; using(var brush=new SolidBrush((e.State&DrawItemState.Selected)!=0?Color.FromArgb(66,70,91):list.BackColor)) e.Graphics.FillRectangle(brush,e.Bounds); TextRenderer.DrawText(e.Graphics,Convert.ToString(list.Items[e.Index]),Font,new Rectangle(e.Bounds.X+6,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix); };
        list.MouseUp+=delegate(object sender,MouseEventArgs e) { if(e.Button==MouseButtons.Left && list.IndexFromPoint(e.Location)>=0) Commit(); };
        overlay.Controls.Add(list); ownerForm.Controls.Add(overlay); overlay.BringToFront();
        ownerForm.Deactivate+=OwnerChanged; ownerForm.Resize+=OwnerChanged; ownerForm.LocationChanged+=OwnerChanged;
        Application.AddMessageFilter(this); list.Focus();
    }
    void OwnerChanged(object sender,EventArgs e) { CloseList(false); }
    void Commit() { int index=list.SelectedIndex; CloseList(true); if(index>=0) SelectedIndex=index; }
    void CloseList(bool focus) {
        if(overlay==null) return;
        Application.RemoveMessageFilter(this); ownerForm.Deactivate-=OwnerChanged; ownerForm.Resize-=OwnerChanged; ownerForm.LocationChanged-=OwnerChanged;
        var old=overlay; overlay=null; list=null; ownerForm.Controls.Remove(old); old.Dispose(); ownerForm=null;
        if(focus && Visible && Enabled) { if(editor.Visible) editor.Focus(); else Focus(); }
    }
    public bool PreFilterMessage(ref Message message) {
        if(overlay!=null) {
            bool click=message.Msg==0x201 || message.Msg==0x204 || message.Msg==0x207 || message.Msg==0x20B || message.Msg==0xA1 || message.Msg==0xA4;
            bool wheel=message.Msg==0x20A;
            if(click || wheel) { Point p=Cursor.Position; if(!RectangleToScreen(ClientRectangle).Contains(p) && !overlay.RectangleToScreen(overlay.ClientRectangle).Contains(p)) CloseList(false); }
            if(message.Msg==0x100 && (Keys)message.WParam.ToInt32()==Keys.Tab) { bool forward=(ModifierKeys&Keys.Shift)==0; CloseList(true); FindForm().SelectNextControl(this,forward,true,true,true); return true; }
        }
        return false;
    }
    protected override void Dispose(bool disposing) { if(disposing) CloseList(false); base.Dispose(disposing); }
}
