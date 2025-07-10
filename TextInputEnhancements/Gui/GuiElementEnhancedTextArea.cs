namespace TextInputEnhancements.Gui;

using System;
using Vintagestory.API.Client;

public class GuiElementEnhancedTextArea : GuiElementTextArea, ITextEnhancements {
	public Enhancements Enhancements { get; }

	public GuiElementEnhancedTextArea(ICoreClientAPI capi, ElementBounds bounds, Action<string> OnTextChanged, CairoFont font) : base(capi, bounds, OnTextChanged, font) {
		this.Enhancements = new Enhancements(this, capi);
	}

	public override void Dispose() {
		this.Enhancements.Dispose();
		base.Dispose();
	}

	public override void OnFocusLost() {
		base.OnFocusLost();
		this.Enhancements.ClearSelection();
	}

	public override void OnKeyDown(ICoreClientAPI api, KeyEvent args) {
		this.Enhancements.OnKeyDown(api, args, base.OnKeyDown);
	}

	public override void OnKeyPress(ICoreClientAPI api, KeyEvent args) {
		this.Enhancements.OnKeyPress(api, args,  base.OnKeyPress);
	}

	public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args) {
		this.Enhancements.OnMouseDownOnElement(api, args, base.OnMouseDownOnElement);
	}

	public override void OnMouseUp(ICoreClientAPI api, MouseEvent args) {
		base.OnMouseUp(api, args);
		this.Enhancements.OnMouseUp(args);
	}

	public override void OnMouseMove(ICoreClientAPI api, MouseEvent args) {
		base.OnMouseMove(api, args);
		this.Enhancements.OnMouseMove(args);
	}

	public override void RenderInteractiveElements(float deltaTime) {
		this.Enhancements.RenderSelection();
		base.RenderInteractiveElements(deltaTime);
	}
}
