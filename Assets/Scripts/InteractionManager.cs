using UnityEngine;

public class InteractionManager : MonoBehaviour
{
    public float interactDistance = 3f;
    private IInteractable currentTarget;

    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                currentTarget = interactable;

                if (Input.GetKeyDown(KeyCode.E))
                    currentTarget.Interact();

                if (Input.GetKey(KeyCode.E))
                    currentTarget.OnHoldE();

                return;
            }
        }
        currentTarget = null;
    }
}