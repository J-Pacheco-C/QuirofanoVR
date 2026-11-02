using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Toma de gases medicinales en la pared (O2, N2O, aire y vacio) y mangueras con codigo de colores
// hasta las entradas de gas de la parte de atras de la maquina.
// Colores: NOM-197-SSA1-2000 define oxigeno = verde y oxido nitroso = azul; el aire medicinal se identifica en amarillo
// y el vacio en blanco (convencion usada en hospitales).
// Todo se calcula con la posicion de la maquina: la pared se busca con un rayo desde la parte de atras de la maquina.
public class GasesMedicinales : MonoBehaviour
{
    [Header("Colores (se pueden ajustar)")]
    public Color colO2 = new Color(0.00f, 0.60f, 0.24f);
    public Color colN2O = new Color(0.04f, 0.22f, 0.68f);
    public Color colAire = new Color(1.00f, 0.82f, 0.00f);
    public Color colVacio = new Color(0.95f, 0.95f, 0.95f);

    [Header("Posicion de la toma")]
    [Tooltip("Altura del centro de la toma sobre el piso, en cm")]
    public float alturaToma = 120f;
    [Tooltip("Tamano de la toma (1 = tamano grande, 0.6 = compacto)")]
    public float escalaToma = 0.6f;
    [Tooltip("Mover la toma a la izquierda (-) o derecha (+) vista de frente a la pared, en cm")]
    public float desplazamientoLateral = 0f;

    [Header("Opciones")]
    [Tooltip("La tercera entrada de la maquina decia 'Reserve O2'; se rotula como AIR (la maquina toma O2, N2O y aire de la pared)")]
    public bool etiquetarAire = true;
    public bool incluirVacio = true;
    [Tooltip("Grosor de las mangueras en cm")]
    public float grosorManguera = 1.5f;

    // entradas de gas del modelo (parte de atras de la maquina)
    const string N_ENTRADA_O2 = "Parts1.082";
    const string N_ENTRADA_AIRE = "Parts1.081";   // en el modelo decia "Reserve O2"
    const string N_ENTRADA_N2O = "Parts1.083";
    const string N_ETIQUETA_AIRE = "Stickers.015";

    float U = 0.01f;
    float K = 0.006f;     // unidad de la toma (U x escalaToma)
    Transform raizMaq;
    Transform panel;
    Vector3 nSala;        // normal de la pared hacia el cuarto
    Func<string, Transform> buscar;

    class Gas
    {
        public string nombre, rotulo;
        public Color color, colorTexto;
        public Transform entrada;     // null = sin manguera (vacio)
        public float r;               // posicion lateral de la entrada
        public float x;               // posicion en la toma (cm)
    }

    public bool Listo { get; private set; }

    static Bounds Caja(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.001f);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    public void Iniciar(Transform raiz, Func<string, Transform> buscador, float unidad, Vector3 frente)
    {
        raizMaq = raiz;
        buscar = buscador;
        U = Mathf.Max(1e-5f, unidad);
        K = U * Mathf.Max(0.2f, escalaToma);
        if (raiz == null) return;

        Transform tO2 = buscar(N_ENTRADA_O2), tAire = buscar(N_ENTRADA_AIRE), tN2O = buscar(N_ENTRADA_N2O);
        if (tO2 == null || tAire == null || tN2O == null)
        {
            Debug.LogWarning("[Gases] No encontre las entradas de gas de la maquina (Parts1.081/082/083).");
            return;
        }

        frente.y = 0f;
        if (frente.sqrMagnitude < 1e-6f) frente = Vector3.forward;
        frente.Normalize();
        Vector3 atras = -frente;

        Vector3 cO2 = Caja(tO2).center, cAire = Caja(tAire).center, cN2O = Caja(tN2O).center;
        Vector3 Pm = (cO2 + cAire + cN2O) / 3f;
        float pisoY = Caja(raiz).min.y;
        float yToma = pisoY + alturaToma * U;

        // ---- pared: el mismo plano donde esta el tomacorriente (SM_Power_2); si no, un rayo desde atras de la maquina ----
        Transform enchufe = BuscarGlobal("SM_Power_2");
        Vector3 puntoPared = Vector3.zero;
        if (enchufe != null) yToma = enchufe.position.y;      // a la misma altura que el tomacorriente
        Vector3 origen = new Vector3(Pm.x, yToma, Pm.z) + atras * (10f * U);
        bool ok = BuscarPared(origen, atras, out puntoPared, out nSala);
        if (!ok && enchufe != null) ok = PlanoDelEnchufe(enchufe, Pm, frente, out puntoPared, out nSala);
        if (!ok)
        {
            Debug.LogWarning("[Gases] No encontre la pared (ni un collider detras de la maquina ni el tomacorriente SM_Power_2); la toma se pone a 60 cm de la maquina.");
            puntoPared = new Vector3(Pm.x, yToma, Pm.z) + atras * (60f * U);
            nSala = frente;
        }
        nSala.y = 0f; nSala.Normalize();

        GameObject go = new GameObject("TomaGases_Pared");
        panel = go.transform;
        panel.rotation = Quaternion.LookRotation(-nSala, Vector3.up);   // +z local = hacia la pared, -z = hacia el cuarto
        panel.position = new Vector3(puntoPared.x, yToma, puntoPared.z) + nSala * 0.001f;
        Vector3 R = panel.right;                                          // derecha de quien mira la pared
        panel.position += R * Vector3.Dot(Pm - panel.position, R);        // centrada detras de las entradas
        panel.position += R * (desplazamientoLateral * U);
        panel.position = new Vector3(panel.position.x, yToma, panel.position.z);

        // ---- que no quede encima del tomacorriente ----
        if (enchufe != null)
        {
            float dr = Vector3.Dot(enchufe.position - panel.position, R);
            float dy = Mathf.Abs(enchufe.position.y - yToma);
            float semiAncho = (incluirVacio ? 45f : 33f) * K + 12f * U;
            if (Mathf.Abs(dr) < semiAncho && dy < 45f * U)
            {
                float desp = (semiAncho - Mathf.Abs(dr)) * (dr > 0f ? -1f : 1f);
                panel.position += R * desp;
            }
        }

        // ---- gases ordenados segun donde esta cada entrada (para que las mangueras no se crucen) ----
        List<Gas> gases = new List<Gas>
        {
            new Gas { nombre = "O2",  rotulo = "O2",   color = colO2,  colorTexto = Color.white, entrada = tO2 },
            new Gas { nombre = "N2O", rotulo = "N2O",  color = colN2O, colorTexto = Color.white, entrada = tN2O },
            new Gas { nombre = "AIR", rotulo = "AIRE", color = colAire, colorTexto = new Color(0.1f, 0.1f, 0.1f), entrada = tAire },
        };
        for (int i = 0; i < gases.Count; i++) gases[i].r = Vector3.Dot(Caja(gases[i].entrada).center - panel.position, R);
        gases.Sort((a, b) => a.r.CompareTo(b.r));
        float sep = 15f;
        for (int i = 0; i < gases.Count; i++) gases[i].x = (i - 1) * sep;
        Gas vacio = null;
        if (incluirVacio)
        {
            vacio = new Gas { nombre = "VAC", rotulo = "VACIO", color = colVacio, colorTexto = new Color(0.1f, 0.1f, 0.1f), entrada = null, x = 2 * sep };
        }

        ConstruirToma(gases, vacio);
        for (int i = 0; i < gases.Count; i++) ConstruirManguera(gases[i], atras, pisoY);
        if (etiquetarAire) EtiquetaAire(frente);
        Listo = true;
        Debug.Log("[Gases] Toma de gases creada. Pared en " + panel.position.ToString("F2") + ", normal " + nSala.ToString("F2") + ", unidad U=" + U.ToString("F4") + ". Se puede ajustar con Desplazamiento Lateral / Altura Toma.");
    }

    // Plan B: la pared pasa por el tomacorriente; su normal es el eje horizontal mas parecido al frente de la maquina
    bool PlanoDelEnchufe(Transform enchufe, Vector3 hacia, Vector3 frente, out Vector3 punto, out Vector3 normal)
    {
        punto = enchufe.position; normal = frente;
        Bounds b = Caja(enchufe);
        Vector3 eje = Mathf.Abs(frente.x) > Mathf.Abs(frente.z) ? Vector3.right : Vector3.forward;
        if (Vector3.Dot(hacia - b.center, eje) < 0f) eje = -eje;     // hacia el cuarto (donde esta la maquina)
        float d = Vector3.Dot(hacia - b.center, eje);
        if (d < 2f * U || d > 600f * U) return false;
        normal = eje;
        punto = hacia - eje * d;
        return true;
    }

    bool BuscarPared(Vector3 origen, Vector3 dir, out Vector3 punto, out Vector3 normal)
    {
        punto = origen; normal = -dir;
        RaycastHit[] hits = Physics.RaycastAll(origen, dir, 800f * U, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue; bool ok = false;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit h = hits[i];
            if (h.transform.IsChildOf(raizMaq)) continue;
            if (panel != null && h.transform.IsChildOf(panel)) continue;
            if (Mathf.Abs(h.normal.y) > 0.5f) continue;
            Vector3 tam = h.collider.bounds.size;
            if (Mathf.Max(tam.x, tam.z) < 250f * U) continue;      // solo paredes grandes (no carritos ni postes)
            if (h.distance < mejor) { mejor = h.distance; punto = h.point; normal = h.normal; ok = true; }
        }
        return ok;
    }

    static Transform BuscarGlobal(string nombre)
    {
        Transform[] todos = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < todos.Length; i++)
            if (string.Equals(todos[i].name, nombre, StringComparison.OrdinalIgnoreCase)) return todos[i];
        return null;
    }

    // ================= materiales y piezas basicas =================
    Material Mat(Color c, float suavidad, float metal)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        Material m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", suavidad);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        return m;
    }

    GameObject Prim(PrimitiveType t, Transform p, Vector3 posU, Vector3 escalaU, Material m, string nombre)
    {
        GameObject g = GameObject.CreatePrimitive(t);
        g.name = nombre;
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(p, false);
        g.transform.localPosition = posU * K;
        g.transform.localScale = escalaU * K;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    GameObject CajaU(Transform p, Vector3 posU, Vector3 tamU, Material m, string nombre)
    {
        return Prim(PrimitiveType.Cube, p, posU, tamU, m, nombre);
    }

    // cilindro con el eje a lo largo de z local (sale de la pared)
    GameObject CilZ(Transform p, Vector3 posU, float radioU, float largoU, Material m, string nombre)
    {
        GameObject g = Prim(PrimitiveType.Cylinder, p, posU, new Vector3(2f * radioU, largoU * 0.5f, 2f * radioU), m, nombre);
        g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        return g;
    }

    TextMeshPro Texto(Transform p, string txt, Vector3 posU, float alturaU, float anchoU, Color c, bool negritas)
    {
        GameObject g = new GameObject("Texto_" + txt.Replace("\n", " "));
        g.transform.SetParent(p, false);
        g.transform.localPosition = posU * K;
        g.transform.localRotation = Quaternion.identity;
        TextMeshPro t = g.AddComponent<TextMeshPro>();
        t.text = txt;
        t.fontSize = alturaU * K * 10f;      // en texto 3D, 1 de tamano ~ 10 cm
        t.color = c;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        if (negritas) t.fontStyle = FontStyles.Bold;
        t.rectTransform.sizeDelta = new Vector2(anchoU * K, alturaU * K * 1.6f);
        return t;
    }

    // ================= toma de pared =================
    void ConstruirToma(List<Gas> gases, Gas vacio)
    {
        float ancho = vacio != null ? 72f : 56f;
        float cx = vacio != null ? 7.5f : 0f;           // centro de la placa respecto a la toma central
        float alto = 26f;

        Material carcasa = Mat(new Color(0.84f, 0.86f, 0.88f), 0.55f, 0.15f);
        Material placa = Mat(new Color(0.70f, 0.73f, 0.77f), 0.5f, 0.3f);
        Material cabecera = Mat(new Color(0.16f, 0.2f, 0.27f), 0.4f, 0.1f);
        Material oscuro = Mat(new Color(0.08f, 0.08f, 0.09f), 0.3f, 0.1f);
        Material metal = Mat(new Color(0.72f, 0.72f, 0.74f), 0.8f, 0.9f);
        Material tornillo = Mat(new Color(0.55f, 0.56f, 0.58f), 0.7f, 0.8f);
        Material ledVerde = Mat(new Color(0.2f, 1f, 0.35f), 0.9f, 0f);
        if (ledVerde.HasProperty("_EmissionColor")) { ledVerde.EnableKeyword("_EMISSION"); ledVerde.SetColor("_EmissionColor", new Color(0.2f, 1f, 0.35f) * 1.6f); }

        // cuerpo y marco (z local: 0 = pared, negativo = hacia el cuarto)
        CajaU(panel, new Vector3(cx, 0f, -2.6f), new Vector3(ancho, alto, 5.2f), carcasa, "Cuerpo");
        CajaU(panel, new Vector3(cx, 0f, -5.5f), new Vector3(ancho - 3f, alto - 3f, 0.8f), placa, "Placa");
        CajaU(panel, new Vector3(cx, alto * 0.5f - 3.2f, -6.1f), new Vector3(ancho - 3f, 4.4f, 0.5f), cabecera, "Cabecera");
        Texto(panel, "GASES MEDICOS", new Vector3(cx - ancho * 0.18f, alto * 0.5f - 3.2f, -6.55f), 1.5f, 30f, Color.white, true);
        Texto(panel, "NOM-197-SSA1", new Vector3(cx + ancho * 0.26f, alto * 0.5f - 3.2f, -6.55f), 1.1f, 22f, new Color(0.75f, 0.8f, 0.9f), false);

        // tornillos en las esquinas
        float sx = ancho * 0.5f - 1.6f, sy = alto * 0.5f - 1.6f;
        for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
                CilZ(panel, new Vector3(cx + a * sx, b * sy, -6.0f), 0.55f, 0.6f, tornillo, "Tornillo");

        // tomas
        List<Gas> todos = new List<Gas>(gases);
        if (vacio != null) todos.Add(vacio);
        for (int i = 0; i < todos.Count; i++)
        {
            Gas g = todos[i];
            Material mc = Mat(g.color, 0.6f, 0.05f);
            float y = -1.2f;
            CilZ(panel, new Vector3(g.x, y, -6.2f), 5.0f, 0.8f, mc, "Collar_" + g.nombre);                 // anillo de color
            CilZ(panel, new Vector3(g.x, y, -6.5f), 3.7f, 0.9f, oscuro, "Hueco_" + g.nombre);
            CilZ(panel, new Vector3(g.x, y, -6.9f), 2.0f, 1.2f, metal, "Boquilla_" + g.nombre);
            CilZ(panel, new Vector3(g.x, y, -7.6f), 0.7f, 0.4f, oscuro, "Centro_" + g.nombre);
            // led de presion
            CilZ(panel, new Vector3(g.x + 4.4f, y + 5.0f, -6.2f), 0.5f, 0.5f, ledVerde, "Led_" + g.nombre);
            // rotulo de color
            CajaU(panel, new Vector3(g.x, -9.2f, -6.0f), new Vector3(11.5f, 3.6f, 0.5f), mc, "Rotulo_" + g.nombre);
            Texto(panel, g.rotulo, new Vector3(g.x, -9.2f, -6.45f), 2.0f, 11f, g.colorTexto, true);
        }
    }

    // ================= mangueras =================
    void ConstruirManguera(Gas g, Vector3 atras, float pisoY)
    {
        Material mh = Mat(g.color, 0.45f, 0.0f);
        Material mhC = Mat(Color.Lerp(g.color, Color.white, 0.25f), 0.6f, 0.1f);
        Material mMetal = Mat(new Color(0.75f, 0.75f, 0.78f), 0.8f, 0.9f);

        GameObject raiz = new GameObject("Manguera_" + g.nombre);
        raiz.transform.SetParent(panel.parent, false);

        // extremo en la toma: punto de la boquilla
        Vector3 boquilla = panel.TransformPoint(new Vector3(g.x, -1.2f, -7.8f) * K);
        Vector3 a = boquilla + nSala * (5.5f * U);
        TuboRecto(raiz.transform, boquilla, a, 1.3f * U, mMetal);
        TuboRecto(raiz.transform, boquilla + nSala * (1.2f * U), boquilla + nSala * (4.8f * U), 1.75f * U, mhC);

        // extremo en la maquina: cara de atras de la entrada
        Bounds bi = Caja(g.entrada);
        float prof = Mathf.Abs(atras.x) * bi.extents.x + Mathf.Abs(atras.y) * bi.extents.y + Mathf.Abs(atras.z) * bi.extents.z;
        Vector3 entradaCara = bi.center + atras * prof;
        Vector3 b = entradaCara + atras * (5.5f * U);
        TuboRecto(raiz.transform, entradaCara, b, 1.3f * U, mMetal);
        TuboRecto(raiz.transform, entradaCara + atras * (1.2f * U), entradaCara + atras * (4.8f * U), 1.75f * U, mhC);

        // manguera: curva con la holgura que cuelga por su peso
        float gap = Mathf.Max(0f, Vector3.Dot(a - b, nSala));      // separacion horizontal entre la pared y la maquina
        float hA = Mathf.Clamp(gap * 0.45f, 3f * U, 22f * U);
        float hB = Mathf.Clamp(gap * 0.45f, 3f * U, 22f * U);
        float dist = Vector3.Distance(a, b);
        Vector3 c1 = a + nSala * hA + Vector3.down * (dist * 0.18f);
        Vector3 c2 = b + atras * hB + Vector3.up * (dist * 0.05f);
        int n = 48;
        LineRenderer lr = raiz.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = n;
        lr.widthMultiplier = grosorManguera * U;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.sharedMaterial = mh;
        float holgura = dist * 0.07f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            Vector3 p = Bezier(a, c1, c2, b, t);
            p.y -= holgura * Mathf.Sin(Mathf.PI * t);
            if (p.y < pisoY + 2f * U) p.y = pisoY + 2f * U;
            lr.SetPosition(i, p);
        }
    }

    static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }

    void TuboRecto(Transform padre, Vector3 de, Vector3 a, float radio, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = "Conector";
        Destroy(g.GetComponent<Collider>());
        Transform t = g.transform;
        t.SetParent(padre, true);
        Vector3 d = a - de;
        float largo = d.magnitude;
        t.position = (de + a) * 0.5f;
        if (largo > 1e-9f) t.rotation = Quaternion.FromToRotation(Vector3.up, d / largo);
        t.localScale = new Vector3(radio * 2f, largo * 0.5f, radio * 2f);
        g.GetComponent<Renderer>().sharedMaterial = m;
    }

    // ================= etiqueta de aire en la maquina =================
    void EtiquetaAire(Vector3 frente)
    {
        Transform st = buscar(N_ETIQUETA_AIRE);
        if (st == null) return;
        Bounds b = Caja(st);
        Vector3 atras = -frente;
        Quaternion rot = Quaternion.LookRotation(frente, Vector3.up);   // lee quien mira la maquina desde atras
        GameObject go = new GameObject("Etiqueta_AIR");
        go.transform.position = b.center + atras * (0.12f * U);
        go.transform.rotation = rot;
        GameObject fondo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fondo.name = "Fondo";
        Destroy(fondo.GetComponent<Collider>());
        fondo.transform.SetParent(go.transform, false);
        fondo.transform.localScale = new Vector3(Mathf.Max(b.size.x, b.size.z) + 0.2f * U, b.size.y + 0.1f * U, 0.15f * U);
        fondo.GetComponent<Renderer>().sharedMaterial = Mat(colAire, 0.3f, 0f);

        GameObject gt = new GameObject("Texto_AIR");
        gt.transform.SetParent(go.transform, false);
        gt.transform.localPosition = new Vector3(0f, 0f, -0.12f * U);   // del lado de quien mira desde atras
        TextMeshPro t = gt.AddComponent<TextMeshPro>();
        t.text = "<size=125%><b>AIR</b></size>\n280kPa-600kPa";
        t.fontSize = 0.95f * U * 10f;
        t.color = new Color(0.1f, 0.1f, 0.1f);
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.rectTransform.sizeDelta = new Vector2(6f * U, 2.8f * U);
    }
}
