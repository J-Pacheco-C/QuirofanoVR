using UnityEngine;

public class SequencePiece : MonoBehaviour, IInteractable
{
    public string pieceId; // si lo dejas vacio usa el nombre del objeto
    public Color colorActivo = Color.red;

    private Renderer rend;
    private Material mat;
    private Color colorOriginal = Color.white;
    private bool usaBaseColor;
    private bool ultimoEstado = false;

    void Start()
    {
        if (string.IsNullOrEmpty(pieceId)) pieceId = gameObject.name;
        rend = GetComponent<Renderer>();
        if (rend == null) return;

        mat = rend.material; // instancia propia
        // Si el shader no soporta color (ej. 2D Mesh), cambiamos a URP/Lit conservando la textura
        if (!mat.HasProperty("_BaseColor") && !mat.HasProperty("_Color"))
        {
            Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                mat = new Material(lit);
                if (tex != null) mat.SetTexture("_BaseMap", tex);
                rend.material = mat;
            }
        }
        usaBaseColor = mat.HasProperty("_BaseColor");
        string prop = usaBaseColor ? "_BaseColor" : "_Color";
        if (mat.HasProperty(prop)) colorOriginal = mat.GetColor(prop);
        Aplicar(false);
    }

    void Update()
    {
        if (mat == null || SequenceManager.Instance == null) return;
        bool turno = SequenceManager.Instance.EsPiezaActual(pieceId);
        if (turno != ultimoEstado) Aplicar(turno);
    }

    void Aplicar(bool activo)
    {
        ultimoEstado = activo;
        string prop = usaBaseColor ? "_BaseColor" : "_Color";
        if (mat.HasProperty(prop)) mat.SetColor(prop, activo ? colorActivo : colorOriginal);
        if (mat.HasProperty("_EmissionColor"))
        {
            if (activo) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", colorActivo * 1.5f); }
            else { mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black); }
        }
    }

    public void Interact()
    {
        // Siempre avisamos al manager: el decide si es correcta o es un error
        SequenceManager.Instance.OnPiezaPresionada(pieceId);
    }

    public void OnHoldE()
    {
        if (!SequenceManager.Instance.EsPiezaActual(pieceId)) return;
        SequenceManager.Instance.OnBotonMantenido(pieceId);
    }

    public void OnHoverEnter() { }
    public void OnHoverExit() { }
}
