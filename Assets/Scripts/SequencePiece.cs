using System.Collections.Generic;
using UnityEngine;

// Pieza interactuable de la rutina. Se pinta de rojo cuando es la que toca usar.
// Funciona con piezas de un solo Renderer y con piezas formadas por varios (por ejemplo el tapon).
public class SequencePiece : MonoBehaviour, IInteractable
{
    public string pieceId; // si lo dejas vacio usa el nombre del objeto
    public Color colorActivo = Color.red;

    class Ranura
    {
        public Material mat;
        public string prop;
        public Color original = Color.white;
    }

    readonly List<Ranura> ranuras = new List<Ranura>();
    bool ultimoEstado = false;

    void Start()
    {
        if (string.IsNullOrEmpty(pieceId)) pieceId = gameObject.name;

        Renderer[] rs = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            Renderer r = rs[i];
            if (r is LineRenderer || r is ParticleSystemRenderer) continue;
            // los renderers que pertenecen a otra pieza de la rutina no se tocan
            if (r.GetComponentInParent<SequencePiece>() != this) continue;

            Material[] ms = r.materials; // instancias propias
            bool cambio = false;
            for (int j = 0; j < ms.Length; j++)
            {
                Material m = ms[j];
                if (m == null) continue;
                // Si el shader no soporta color (ej. 2D Mesh), cambiamos a URP/Lit conservando la textura
                if (!m.HasProperty("_BaseColor") && !m.HasProperty("_Color"))
                {
                    Texture tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                    Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                    if (lit != null)
                    {
                        Material nuevo = new Material(lit);
                        if (tex != null) nuevo.SetTexture("_BaseMap", tex);
                        ms[j] = m = nuevo;
                        cambio = true;
                    }
                }
                Ranura rn = new Ranura();
                rn.mat = m;
                rn.prop = m.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                if (m.HasProperty(rn.prop)) rn.original = m.GetColor(rn.prop);
                ranuras.Add(rn);
            }
            if (cambio) r.materials = ms;
        }
        Aplicar(false);
    }

    void Update()
    {
        if (ranuras.Count == 0 || SequenceManager.Instance == null) return;
        bool turno = SequenceManager.Instance.EsPiezaActual(pieceId);
        if (turno != ultimoEstado) Aplicar(turno);
    }

    void Aplicar(bool activo)
    {
        ultimoEstado = activo;
        for (int i = 0; i < ranuras.Count; i++)
        {
            Ranura rn = ranuras[i];
            Material mat = rn.mat;
            if (mat == null) continue;
            if (mat.HasProperty(rn.prop)) mat.SetColor(rn.prop, activo ? colorActivo : rn.original);
            if (mat.HasProperty("_EmissionColor"))
            {
                if (activo) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", colorActivo * 1.5f); }
                else { mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black); }
            }
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
