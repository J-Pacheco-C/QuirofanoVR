public interface IInteractable
{
    void Interact();
    void OnHoverEnter();
    void OnHoverExit();
    void OnHoldE(); // NUEVO: se llama cada frame mientras se mantiene presionada E
}