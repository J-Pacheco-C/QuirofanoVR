using UnityEngine;

public class MovablePart : MonoBehaviour
{
    private Vector3 targetLocalPos;
    private float speed = 0.05f;
    private bool moving = false;

    // Movimiento automatico (no necesita mantener E)
    public void MoveTowards(Vector3 targetLocal, float moveSpeed)
    {
        targetLocalPos = targetLocal;
        speed = moveSpeed;
        moving = true;
    }

    // Movimiento manual: se llama cada frame mientras se mantiene E.
    // Regresa true cuando ya llego al destino.
    public bool Step(Vector3 targetLocal, float moveSpeed)
    {
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetLocal, moveSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.localPosition, targetLocal) < 0.001f)
        {
            transform.localPosition = targetLocal;
            return true;
        }
        return false;
    }

    void Update()
    {
        if (!moving) return;

        transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetLocalPos, speed * Time.deltaTime);

        if (Vector3.Distance(transform.localPosition, targetLocalPos) < 0.001f)
        {
            transform.localPosition = targetLocalPos;
            moving = false;
            SequenceManager.Instance.OnPartReachedTarget();
        }
    }
}
