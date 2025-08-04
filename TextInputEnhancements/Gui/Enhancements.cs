namespace TextInputEnhancements.Gui;

using Cairo;
using System;
using System.Collections.Generic;
using System.Reflection;
using TextInputEnhancements.Extensions;
using TextInputEnhancements.Patches;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

public interface ITextEnhancements {
	Enhancements Enhancements { get; }
}

public class Enhancements : IDisposable {
	private const int DoubleClickMilliseconds = 400;
	private static readonly FieldInfo leftPaddingField = typeof(GuiElementEditableTextBase).GetNonPublicField("leftPadding");
	private static readonly FieldInfo linesField = typeof(GuiElementEditableTextBase).GetNonPublicField("lines");
	private static readonly FieldInfo topPaddingField = typeof(GuiElementEditableTextBase).GetNonPublicField("topPadding");

	private readonly GuiElementEditableTextBase parent;
	private readonly ICoreClientAPI api;
	private bool handlingOnKeyEvent;
	private bool mouseDown;
	private int lastClickCursor;
	private long lastClickTime;
	private int? selectionStart;
	private readonly int selectionTextureId;

	private List<string> Lines => Enhancements.linesField.GetValue(this.parent) as List<string>;
	private string Text => string.Join("", this.Lines);

	public Enhancements(GuiElementEditableTextBase parent, ICoreClientAPI api) {
		this.parent = parent;
		this.api = api;
		this.selectionTextureId = Enhancements.GenerateSelectionTexture(api);
	}

	public virtual void Dispose() {
		this.api.Gui.DeleteTexture(this.selectionTextureId);
	}

	public void ClearSelection() {
		this.selectionStart = null;
	}

	public void OnKeyDown(ICoreClientAPI api, KeyEvent e, Action<ICoreClientAPI, KeyEvent> baseFunc) {
		if(!this.parent.HasFocus) {
			baseFunc(api, e);
			return;
		}

		this.handlingOnKeyEvent = true;
		this.OnKeyDownInternal(api, e, baseFunc);
		this.handlingOnKeyEvent = false;
	}

	public void OnKeyPress(ICoreClientAPI api, KeyEvent e, Action<ICoreClientAPI, KeyEvent> baseFunc) {
		if(!this.parent.HasFocus) {
			baseFunc(api, e);
			return;
		}

		this.handlingOnKeyEvent = true;
		this.OnKeyPressInternal(api, e, baseFunc);
		this.handlingOnKeyEvent = false;
	}

	public void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent e, Action<ICoreClientAPI, MouseEvent> baseFunc) {
		if(e.Button != EnumMouseButton.Left) {
			if(!this.mouseDown)
				this.selectionStart = null;
			baseFunc(api, e);
			return;
		}

		bool shiftDown = api.Input.KeyboardKeyStateRaw[(int)GlKeys.ShiftLeft] || api.Input.KeyboardKeyStateRaw[(int)GlKeys.ShiftRight];
		if(shiftDown && this.selectionStart == null)
			this.selectionStart = this.parent.CaretPosWithoutLineBreaks;
		baseFunc(api, e);

		if(!shiftDown) {
			long now = api.ElapsedMilliseconds;
			int caretPos = this.parent.CaretPosWithoutLineBreaks;
			bool isDoubleClick = this.lastClickCursor == caretPos && now - this.lastClickTime < Enhancements.DoubleClickMilliseconds;
			this.lastClickCursor = caretPos;
			this.lastClickTime = now;

			if(isDoubleClick) {
				this.SelectWordAtCursor();
				return;
			}
			this.selectionStart = caretPos;
		}
		this.mouseDown = true;
	}

	public void OnMouseUp(MouseEvent e) {
		if(e.Button != EnumMouseButton.Left)
			return;

		this.mouseDown = false;
		if(this.selectionStart == this.parent.CaretPosWithoutLineBreaks)
			this.selectionStart = null;
	}

	public void OnMouseMove(MouseEvent e) {
		if(this.mouseDown)
			this.parent.SetCaretPos((double)e.X - this.parent.Bounds.absX, (double)e.Y - this.parent.Bounds.absY);
	}

	public void OnTextChanged() {
		if(this.handlingOnKeyEvent)
			return;

		this.selectionStart = null;
	}

	private void OnKeyDownInternal(ICoreClientAPI api, KeyEvent e, Action<ICoreClientAPI, KeyEvent> baseFunc) {
		if(e.AltPressed) { // All shortcuts should work only if Alt is not pressed.
			e.Handled = true; // Textfield should consume all Alt actions, otherwise we would start to move when typing with AltGr.
			return;
		}

		if((e.CtrlPressed || e.CommandPressed) && this.OnControlAction(e)) {
			e.Handled = true;
			return;
		}

		if(e.IsDeleteCategoryKey()) {
			if(this.selectionStart == null)
				baseFunc(api, e);
			else {
				this.DeleteSelectedText(this.parent.CaretPosWithoutLineBreaks, 0);
				e.Handled = true;
			}
			return;
		}

		if(e.IsCursorMovementKey() && e.ShiftPressed != this.selectionStart.HasValue)
			this.selectionStart = e.ShiftPressed ? this.parent.CaretPosWithoutLineBreaks : null;
		this.OnKeyPressInternal(api, e, baseFunc);
	}

	private bool OnControlAction(KeyEvent e) {
		string keyString = GlKeyNames.GetPrintableChar(e.KeyCode); // we want layout-independent keys
		if(keyString == "a") {
			this.selectionStart = 0;
			this.parent.SetCaretPos(int.MaxValue, int.MaxValue);
			return true;
		}
		if(keyString == "c" || keyString == "x")
			return this.OnCopyCut(keyString == "c" ? CopyCutMode.Copy : CopyCutMode.Cut);
		if(keyString == "v")
			return this.OnPaste();
		if(e.IsDeleteCategoryKey())
			return this.OnDeleteWord(e.KeyCode == (int)GlKeys.BackSpace ? -1 : 1);
		return false;
	}

	private enum CopyCutMode { Copy, Cut }
	private bool OnCopyCut(CopyCutMode mode) {
		if(this.selectionStart == null)
			return false;

		string text = this.Text;
		int caretPos = this.parent.CaretPosWithoutLineBreaks;
		(int start, int end) = this.GetSelection(caretPos);
		string subtext = text[start..end];
		if(subtext.Length != 0)
			this.api.Input.ClipboardText = subtext;
		if(mode == CopyCutMode.Cut)
			this.DeleteSelectedText(caretPos, 0);
		return true;
	}

	private bool OnPaste() {
		if(this.selectionStart != null)
			this.DeleteSelectedText(this.parent.CaretPosWithoutLineBreaks, 0);

		string text = this.Text;
		string insert = this.api.Input.ClipboardText.Replace("\uFEFF", null);
		int caretPos = this.parent.CaretPosWithoutLineBreaks;
		this.parent.SetValue(text[..caretPos] + insert + text[caretPos..], false);
		this.parent.CaretPosWithoutLineBreaks = caretPos + insert.Length;
		return true;
	}

	private bool OnDeleteWord(int direction) {
		if(this.selectionStart != null)
			return false;

		this.selectionStart = this.parent.CaretPosWithoutLineBreaks;
		this.parent.MoveCursorWholeWord(direction, true);
		this.DeleteSelectedText(this.parent.CaretPosWithoutLineBreaks, 0);
		return true;
	}

	private void OnKeyPressInternal(ICoreClientAPI api, KeyEvent e, Action<ICoreClientAPI, KeyEvent> baseFunc) {
		int originalCaretPos = this.parent.CaretPosWithoutLineBreaks;
		int originalTextLength = this.parent.TextLengthWithoutLineBreaks;
		baseFunc(api, e);

		int newCaretPos = this.parent.CaretPosWithoutLineBreaks;
		if(!this.mouseDown && this.selectionStart == newCaretPos)
			this.selectionStart = null;
		if(this.selectionStart == null || this.parent.TextLengthWithoutLineBreaks == originalTextLength)
			return;

		if(originalCaretPos < this.selectionStart) {
			this.selectionStart += newCaretPos - originalCaretPos;
			this.DeleteSelectedText(newCaretPos, newCaretPos - originalCaretPos);
		}
		else
			this.DeleteSelectedText(originalCaretPos, newCaretPos - originalCaretPos);
	}

	private void SelectWordAtCursor() {
		static int GetCharRank(char c) {
			if(Enhancements.IsWordChar(c))
				return 3;
			if(!char.IsWhiteSpace(c))
				return 2;
			return 1;
		}

		string line = this.Lines[this.parent.CaretPosLine];
		int caretPosInLine = this.parent.CaretPosInLine;
		int targetRank = caretPosInLine > 0 ? GetCharRank(line[caretPosInLine - 1]) : 0;
		if(caretPosInLine < line.Length)
			targetRank = Math.Max(targetRank, GetCharRank(line[caretPosInLine]));

		int start = caretPosInLine;
		while(start > 0 && GetCharRank(line[start - 1]) == targetRank)
			--start;

		int end = caretPosInLine;
		while(end < line.Length && GetCharRank(line[end]) == targetRank)
			++end;

		if(start == caretPosInLine && end == caretPosInLine)
			return;
		this.selectionStart = this.parent.CaretPosWithoutLineBreaks - (caretPosInLine - start);
		this.parent.SetCaretPos(end, this.parent.CaretPosLine);
	}

	private void DeleteSelectedText(int caretPos, int caretPosOffset) {
		(int start, int end) = this.GetSelection(caretPos);
		this.parent.SetValue(this.Text.Remove(start, end - start), false);
		this.selectionStart = null;
		if(caretPos == end)
			this.parent.CaretPosWithoutLineBreaks = start + caretPosOffset;
	}

	private (int, int) GetSelection(int caretPos) {
		return (Math.Min(this.selectionStart.Value, caretPos), Math.Max(this.selectionStart.Value, caretPos));
	}

	public void RenderSelection() {
		if(this.selectionStart == null)
			return;

		List<string> lines = this.Lines;
		double renderX = this.parent.Bounds.renderX + (double)Enhancements.leftPaddingField.GetValue(this.parent);
		double renderY = this.parent.Bounds.renderY + (double)Enhancements.topPaddingField.GetValue(this.parent);
		double height = this.parent.Font.GetFontExtents().Height;

		void RenderSelectionLine(int fromX, int toX, int lineIndex) {
			double x = renderX + (fromX == 0 ? 0 : this.parent.Font.GetTextExtents(lines[lineIndex][..fromX]).XAdvance);
			double y = renderY + height * lineIndex;
			double width = this.parent.Font.GetTextExtents(lines[lineIndex].Substring(fromX, (toX == -1 ? lines[lineIndex].Length : toX) - fromX)).XAdvance;
			this.api.Render.Render2DTexturePremultipliedAlpha(this.selectionTextureId, x, y, width, height);
		}
		(int, int) GetPosition(int positionWithoutLineBreaks) {
			int linePos = 0;
			foreach(string line in lines)
				if(positionWithoutLineBreaks > line.Length) {
					++linePos;
					positionWithoutLineBreaks -= line.Length;
				}
				else
					break;
			return (positionWithoutLineBreaks, linePos);
		}

		int caretPos = this.parent.CaretPosWithoutLineBreaks;
		(int startX, int startY) = GetPosition(Math.Min(this.selectionStart.Value, caretPos));
		(int endX, int endY) = GetPosition(Math.Max(this.selectionStart.Value, caretPos));
		if(startY == endY)
			RenderSelectionLine(startX, endX, startY);
		else {
			RenderSelectionLine(startX, -1, startY);
			for(int lineIndex = startY + 1; lineIndex < endY; ++lineIndex)
				RenderSelectionLine(0, -1, lineIndex);
			RenderSelectionLine(0, endX, endY);
		}
	}

	private static int GenerateSelectionTexture(ICoreClientAPI api) {
		using ImageSurface surface = new(Format.Argb32, 32, 32);
		using Context context = new(surface);
		context.SetSourceRGBA(0, 0.75, 1, 0.5);
		context.Paint();
		return api.Gui.LoadCairoTexture(surface, true);
	}

	internal static bool IsWordChar(char c) => c == '_' || char.IsLetterOrDigit(c);
}
