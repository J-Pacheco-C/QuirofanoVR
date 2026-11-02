using UnityEngine;

// Dibuja un cable desde la terminal de la maquina (SM_POWER_1) hasta el enchufe (SM_POWER_2).
// No necesitas configurar nada: se crea solo al dar Play. Si prefieres, puedes
// agregarlo a cualquier objeto vacio y ajustar los valores en el Inspector.
public class CableElectrico : MonoBehaviour
{
    [Header("Extremos (por nombre)")]
    public string nombreOrigen = "SM_POWER_1";   // terminal de la maquina
    public string nombreDestino = "SM_POWER_2";  // enchufe de pared
    public Transform origen;                     // opcional: arrastrar a mano
    public Transform destino;                    // opcional: arrastrar a mano
    public Vector3 desfaseOrigen = Vector3.zero; // ajuste fino (mundo, metros)
    public Vector3 desfaseDestino = Vector3.zero;

    [Header("Forma del cable")]
    [Range(8, 120)] public int segmentos = 48;
    public bool grosorAutomatico = true;     // el grosor se calcula con el tamano de la clavija
    public float grosor = 0.04f;             // se usa si Grosor Automatico esta apagado
    public float holgura = 0.30f;        // caida minima en medio (m)
    [Range(0f, 0.5f)] public float holguraRelativa = 0.18f; // caida como fraccion del largo del cable
    public float salidaRecta = 0.10f;    // tramo recto al salir de cada conector (m)
    public bool limitarAlPiso = true;
    public float alturaPiso = 0.02f;     // Y minima del cable
    public Color color = new Color(0.04f, 0.04f, 0.04f);

    [Header("Avanzado")]
    public bool actualizarEnVivo = false; // true si la maquina se mueve

    LineRenderer lr;
    Material mat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Auto()
    {
        if (Object.FindFirstObjectByType<CableElectrico>() == null)
            new GameObject("CableElectrico").AddComponent<CableElectrico>();
    }

    void Start()
    {
        if (origen == null) origen = Buscar(nombreOrigen);
        if (destino == null) destino = Buscar(nombreDestino);

        if (origen == null || destino == null)
        {
            Debug.LogWarning("[Cable] No encontre " + (origen == null ? nombreOrigen : "") + " " +
                             (destino == null ? nombreDestino : "") +
                             ". Revisa el nombre exacto en la Hierarchy o arrastralos en el Inspector.");
            enabled = false;
            return;
        }

        lr = gameObject.GetComponent<LineRenderer>();
        if (lr == null) lr = gameObject.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.numCapVertices = 6;
        lr.numCornerVertices = 6;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        lr.receiveShadows = true;

        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        mat = new Material(sh);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
        lr.sharedMaterial = mat;

        Construir();
    }

    void LateUpdate()
    {
        if (actualizarEnVivo && lr != null) Construir();
    }

    [ContextMenu("Reconstruir cable")]
    void Construir()
    {
        Vector3 ca = Centro(origen);
        Vector3 cb = Centro(destino);

        // Punta libre de cada clavija (el extremo mas cercano al otro objeto) y su direccion de salida
        Vector3 sa, sb;
        Vector3 a = Extremo(origen, cb, out sa) + desfaseOrigen;
        Vector3 b = Extremo(destino, ca, out sb) + desfaseDestino;

        Vector3 a2 = a + sa * salidaRecta;
        Vector3 b2 = b + sb * salidaRecta;
        float caida = Mathf.Max(holgura, Vector3.Distance(a, b) * holguraRelativa);

        int n = Mathf.Max(8, segmentos);
        lr.positionCount = n + 3;
        lr.SetPosition(0, a);
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            Vector3 p = Vector3.Lerp(a2, b2, t);
            p.y -= caida * 4f * t * (1f - t);          // parabola colgante
            if (limitarAlPiso && p.y < alturaPiso) p.y = alturaPiso;
            lr.SetPosition(i + 1, p);
        }
        lr.SetPosition(n + 2, b);
        float gr = grosor;
        if (grosorAutomatico)
        {
            Vector3 so = Caja(origen).size, sd = Caja(destino).size;
            float largo = Mathf.Min(Mathf.Max(so.x, Mathf.Max(so.y, so.z)), Mathf.Max(sd.x, Mathf.Max(sd.y, sd.z)));
            gr = Mathf.Max(0.0005f, largo * 0.13f);   // el cable mide ~13% del largo de la clavija
        }
        lr.widthMultiplier = gr;
    }

    static Bounds Caja(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.01f);
        Bounds bb = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) bb.Encapsulate(rs[i].bounds);
        return bb;
    }

    static Vector3 Centro(Transform t) { return Caja(t).center; }

    // Devuelve la punta de la clavija (eje mas largo) que queda mas cerca de "otro"
    // y la direccion en que debe salir el cable.
    static Vector3 Extremo(Transform t, Vector3 otro, out Vector3 salida)
    {
        Bounds bb = Caja(t);
        Vector3 s = bb.size;
        int eje = (s.x >= s.y && s.x >= s.z) ? 0 : (s.y >= s.z ? 1 : 2);
        Vector3 dir = eje == 0 ? Vector3.right : (eje == 1 ? Vector3.up : Vector3.forward);
        float mitad = s[eje] * 0.5f;
        Vector3 p1 = bb.center + dir * mitad;
        Vector3 p2 = bb.center - dir * mitad;
        bool uno = (p1 - otro).sqrMagnitude <= (p2 - otro).sqrMagnitude;
        salida = uno ? dir : -dir;
        return bb.center + salida * (mitad * 0.85f);   // un poco adentro para que no se vea hueco
    }

    static Transform Buscar(string nombre)
    {
        var todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in todos)
            if (string.Equals(t.name, nombre, System.StringComparison.OrdinalIgnoreCase)) return t;
        foreach (var t in todos) // por si el nombre trae sufijos como "SM_POWER_1 (1)"
            if (t.name.StartsWith(nombre, System.StringComparison.OrdinalIgnoreCase)) return t;
        return null;
    }
}
