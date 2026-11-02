using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Mueve y crea las piezas visuales del circuito respiratorio:
// manometro (aguja), palanca Bag/Vent, perillas, flotador del flujometro,
// bolsa/pulmon de prueba, fuelle (concertina), pieza Y y tapon de la Y.
// Los tamanos de lo que se crea se calculan con la distancia entre las dos
// conexiones azules del modelo, asi que funciona con cualquier escala.
public class CircuitoVisual : MonoBehaviour
{
    [Header("Palanca Bag/Vent")]
    [Tooltip("Cuantos grados gira de VENT a BAG (positivo = hacia el frente de la maquina; negativo = hacia la concertina)")]
    public float angPalancaBag = 60f;

    [Header("Pieza Y (se ajusta si no queda bien)")]
    [Tooltip("Hacia donde sale la Y: 0 = al frente de la maquina, positivo = hacia la izquierda")]
    public float giroSalidaY = 45f;
    [Tooltip("Largo del tubo de la Y (en unidades del modelo)")]
    public float largoSalidaY = 6f;
    [Tooltip("Donde cuelga el tapon: (hacia afuera, lado, arriba) en unidades del modelo")]
    public Vector3 reposoTapon = new Vector3(5f, 0f, -7f);

    // ---------- nombres de piezas del modelo ----------
    public const string N_AGUJA = "Parts1.024";
    public const string N_ESFERA = "Stickers.019";
    public const string N_LENTE = "Glass1.015";
    public const string N_PERILLA_O2 = "Parts1.010";
    public const string N_STICKER_O2 = "Stickers.013";
    public const string N_PERILLA_APL = "Parts1.043";
    public const string N_STICKER_APL = "Stickers.014";
    public const string N_PALANCA = "Parts1.013";
    public const string N_PIVOTE_PALANCA = "Parts1.029";
    public const string N_FLOTADOR = "Parts1.055";
    public const string N_TUBO = "Glass1.007";
    public const string N_BOLSA = "Parts1.032";
    public const string N_FUELLE = "Parts1.051";
    public const string N_CARCASA_FUELLE = "Glass1.004";
    public const string N_CONECTOR_A = "Parts1.052";
    public const string N_CONECTOR_B = "Parts1.053";
    public const string N_FLUSH = "Stickers.018";

    public const string ID_TAPON = "TaponY";
    public const string ID_BOLSA = N_BOLSA;   // la bolsa del modelo se usa tambien como pulmon de prueba

    Func<string, Transform> buscar;
    Transform raiz;

    // ---------- Aguja del manometro ----------
    Transform aguja;
    Vector3 agujaPos0, dialCentro, dialEje;
    Quaternion agujaRot0;

    // ---------- Perillas / palanca (giro absoluto, sin acumular error) ----------
    class GrupoGiro
    {
        public Transform[] t;
        public Vector3[] pos;
        public Quaternion[] rot;
        public Vector3 centro, eje;
        public float ang;
        public Coroutine co;
    }
    GrupoGiro gO2, gAPL, gPal;

    // ---------- Flotador ----------
    Transform flotador;
    Vector3 flotadorPos0;
    float flotadorRecorrido;

    // ---------- Fuelle (concertina) ----------
    Transform fuelle;
    Vector3 fuelleEscala0, fuellePos0;
    float fuelleBaseY, fuelleSVacio = 0.3f, fuelleSLleno = 1.2f;

    // ---------- Bolsa ----------
    Transform bolsa;
    Vector3 bolsaEscala0, bolsaPos0, bolsaTop0;
    Quaternion bolsaRot0;
    float bagS = 0.55f;          // ancho
    float bagL = 0.55f;          // largo (hacia abajo)
    int bagEje = 1;              // eje local del largo de la bolsa
    Vector3 bagDirL0 = Vector3.up;

    // ---------- Y, tapon ----------
    Transform yRoot, tapon;
    LineRenderer cordonTapon;
    Vector3 yUnion, yPuerto, yOut, yLado;
    float U = 0.01f;
    Material matCordon;
    Transform codo;
    Vector3 codoFin;

    // ---------- Objetos que se agarran ----------
    class Obj
    {
        public string id;
        public Transform t;
        public bool esBolsa;
        public Vector3 restRef, placedRef, curRef;
        public Quaternion restRot, placedRot, curRot;
        public bool puesto;          // false = en su lugar de reposo, true = puesto en la Y
        public float iman, encaje;   // radios (m) para la ayuda de encaje
        public Coroutine co;
    }
    Obj oTapon, oBolsa;

    // Zona de destino (esfera verde)
    GameObject ghost;
    Material matGhost;
    float ghostRadio;

    public bool Listo { get; private set; }
    public float Unidad { get { return U; } }
    public Transform Tapon { get { return tapon; } }
    // Hacia donde mira la maquina (horizontal), segun la esfera del manometro
    public Vector3 Frente
    {
        get
        {
            Vector3 f = Vector3.ProjectOnPlane(dialEje, Vector3.up);
            return f.sqrMagnitude < 1e-6f ? Vector3.forward : f.normalized;
        }
    }
    public Transform Bolsa { get { return bolsa; } }

    // ================= Inicio =================
    public void Iniciar(Transform raizMaquina, Func<string, Transform> buscador)
    {
        raiz = raizMaquina;
        buscar = buscador;
        IniciarAguja();
        IniciarGrupos();
        IniciarFlotador();
        IniciarFuelle();
        IniciarBolsa();
        CrearYTapon();
        Listo = true;
        Reiniciar();
    }

    static Bounds Caja(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.001f);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    void Falta(string n) { Debug.LogWarning("[Circuito] No encontre la pieza " + n); }

    // ---------- Manometro ----------
    void IniciarAguja()
    {
        aguja = buscar(N_AGUJA);
        Transform cara = buscar(N_ESFERA);
        Transform lente = buscar(N_LENTE);
        if (aguja == null) { Falta(N_AGUJA); return; }
        if (cara == null) { Falta(N_ESFERA); aguja = null; return; }
        dialCentro = Caja(cara).center;
        Vector3 n = lente != null ? Caja(lente).center - dialCentro : Vector3.zero;
        if (n.sqrMagnitude < 1e-12f) n = cara.TransformDirection(Vector3.forward);
        dialEje = n.normalized;
        agujaPos0 = aguja.position;
        agujaRot0 = aguja.rotation;
    }

    // Escala de la esfera: -20 a 100 cmH2O en 270 grados (2.25 grados por cmH2O).
    // En 0 cmH2O la aguja apunta a las 9 en punto; sube girando a la derecha.
    public void SetAguja(float cmH2O)
    {
        if (aguja == null) return;
        aguja.position = agujaPos0;
        aguja.rotation = agujaRot0;
        aguja.RotateAround(dialCentro, dialEje, cmH2O * 2.25f);
    }

    // ---------- Perillas y palanca ----------
    void IniciarGrupos()
    {
        Transform pO2 = buscar(N_PERILLA_O2), sO2 = buscar(N_STICKER_O2);
        Transform pAPL = buscar(N_PERILLA_APL), sAPL = buscar(N_STICKER_APL);
        Transform pal = buscar(N_PALANCA), piv = buscar(N_PIVOTE_PALANCA);
        if (pO2 == null) Falta(N_PERILLA_O2);
        if (pAPL == null) Falta(N_PERILLA_APL);
        if (pal == null) Falta(N_PALANCA);

        gO2 = NuevoGrupo(new Transform[] { pO2, sO2 });
        gAPL = NuevoGrupo(new Transform[] { pAPL, sAPL });
        gPal = NuevoGrupo(new Transform[] { pal });

        Vector3 c, e;
        EjeDePerilla(pO2, sO2, out c, out e); gO2.centro = c; gO2.eje = e;
        EjeDePerilla(pAPL, sAPL, out c, out e); gAPL.centro = c; gAPL.eje = e;
        // la palanca Bag/Vent gira en vertical alrededor de su base
        if (pal != null)
        {
            gPal.centro = piv != null ? Caja(piv).center : Caja(pal).center;
            gPal.eje = Vector3.up;
        }
    }

    static GrupoGiro NuevoGrupo(Transform[] ts)
    {
        List<Transform> l = new List<Transform>();
        for (int i = 0; i < ts.Length; i++) if (ts[i] != null) l.Add(ts[i]);
        GrupoGiro g = new GrupoGiro();
        g.t = l.ToArray();
        g.pos = new Vector3[g.t.Length];
        g.rot = new Quaternion[g.t.Length];
        for (int i = 0; i < g.t.Length; i++) { g.pos[i] = g.t[i].position; g.rot[i] = g.t[i].rotation; }
        g.ang = 0f;
        return g;
    }

    // Eje = de la perilla hacia su etiqueta (sale de la cara); centro = centro de la etiqueta
    void EjeDePerilla(Transform cuerpo, Transform sticker, out Vector3 centro, out Vector3 eje)
    {
        centro = Vector3.zero; eje = Vector3.up;
        if (cuerpo == null) return;
        Vector3 cc = Caja(cuerpo).center;
        if (sticker != null)
        {
            centro = Caja(sticker).center;
            Vector3 d = centro - cc;
            if (d.sqrMagnitude > 1e-12f) { eje = d.normalized; return; }
        }
        centro = cc;
        Vector3 s = Caja(cuerpo).size;
        eje = (s.y <= s.x && s.y <= s.z) ? Vector3.up : (s.z <= s.x ? Vector3.forward : Vector3.right);
    }

    void AplicarAng(GrupoGiro g, float a)
    {
        for (int i = 0; i < g.t.Length; i++)
        {
            g.t[i].position = g.pos[i];
            g.t[i].rotation = g.rot[i];
            g.t[i].RotateAround(g.centro, g.eje, a);
        }
        g.ang = a;
    }

    IEnumerator IrAng(GrupoGiro g, float objetivo, float dur)
    {
        float a0 = g.ang, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            AplicarAng(g, Mathf.Lerp(a0, objetivo, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur))));
            yield return null;
        }
        AplicarAng(g, objetivo);
    }

    void Girar(GrupoGiro g, float objetivo, float dur)
    {
        if (g == null || g.t.Length == 0) return;
        if (g.co != null) StopCoroutine(g.co);
        g.co = StartCoroutine(IrAng(g, objetivo, dur));
    }

    // Flujo de O2: cerrado = 0 grados, abierto = 300 grados en sentido antihorario (visto de frente)
    public void GirarO2(bool abrir) { Girar(gO2, abrir ? -300f : 0f, 0.9f); }
    // APL: abierta (SP) = 0 grados, cerrada = 270 grados a favor de las manecillas (visto desde arriba)
    public void GirarAPL(bool cerrar) { Girar(gAPL, cerrar ? 270f : 0f, 1.1f); }
    // Palanca: VENT = posicion inicial, BAG = angPalancaBag grados hacia el frente
    public void GirarPalanca(bool aVent) { Girar(gPal, aVent ? 0f : angPalancaBag, 0.6f); }

    // ---------- Flotador del flujometro ----------
    void IniciarFlotador()
    {
        Transform orig = buscar(N_FLOTADOR);
        Transform tubo = buscar(N_TUBO);
        if (orig == null || tubo == null) { Falta(orig == null ? N_FLOTADOR : N_TUBO); return; }

        // la pieza original del modelo parece dos bolitas pegadas: se oculta y se usa una sola esfera gris, pequena
        Renderer[] ro = orig.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < ro.Length; i++) ro[i].enabled = false;

        Bounds bt = Caja(tubo);
        float D = Mathf.Min(bt.size.x, bt.size.z);        // diametro del tubo
        float d = 0.6f * D;                               // diametro de la bolita
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = "Balin_Flujometro";
        Destroy(g.GetComponent<Collider>());
        flotador = g.transform;
        flotador.SetParent(orig.parent, true);
        flotador.position = new Vector3(bt.center.x, bt.min.y + 0.5f * D + 0.5f * d, bt.center.z);
        float ps = Mathf.Max(1e-6f, Mathf.Abs(orig.parent != null ? orig.parent.lossyScale.y : 1f));
        flotador.localScale = Vector3.one * (d / ps);
        flotadorPos0 = flotador.position;
        flotadorRecorrido = Mathf.Max(0f, bt.size.y - D - d) * 0.9f;

        Color col = new Color(0.55f, 0.57f, 0.6f);
        Renderer r = g.GetComponent<Renderer>();
        // el vidrio del tubo tapa lo que hay dentro: este material lo dibuja siempre encima
        Shader sh = Shader.Find("Quirofano/ZAlways");
        Material m;
        if (sh != null) { m = new Material(sh); m.SetColor("_Color", col); }
        else
        {
            sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh != null ? sh : Shader.Find("Standard"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            m.renderQueue = 3100;
        }
        r.sharedMaterial = m;
    }

    // k = 0..1 (0 = abajo, sin flujo)
    public void SetFlotador(float k)
    {
        if (flotador == null) return;
        flotador.position = flotadorPos0 + Vector3.up * (flotadorRecorrido * Mathf.Clamp01(k));
    }

    // ---------- Fuelle (concertina) ----------
    void IniciarFuelle()
    {
        fuelle = buscar(N_FUELLE);
        if (fuelle == null) { Falta(N_FUELLE); return; }
        fuelleEscala0 = fuelle.localScale;
        fuellePos0 = fuelle.position;
        Bounds b = Caja(fuelle);
        fuelleBaseY = b.min.y;                       // el fuelle se estira hacia arriba desde su base
        Transform carc = buscar(N_CARCASA_FUELLE);
        if (carc != null)
        {
            Bounds cb = Caja(carc);
            float tope = cb.max.y - 0.06f * cb.size.y;   // casi hasta arriba de la carcasa transparente
            fuelleSLleno = Mathf.Max(1f, (tope - fuelleBaseY) / Mathf.Max(1e-6f, b.size.y));
        }
    }

    // k = 0 (vacio) .. 1 (lleno hasta arriba)
    public void SetFuelle(float k)
    {
        if (fuelle == null) return;
        float s = Mathf.Lerp(fuelleSVacio, fuelleSLleno, Mathf.Clamp01(k));
        fuelle.localScale = new Vector3(fuelleEscala0.x, fuelleEscala0.y * s, fuelleEscala0.z);
        Vector3 p = fuellePos0;
        p.y = fuellePos0.y + (1f - s) * (fuelleBaseY - fuellePos0.y);
        fuelle.position = p;
    }

    // ---------- Bolsa (tambien sirve de pulmon de prueba) ----------
    void IniciarBolsa()
    {
        bolsa = buscar(N_BOLSA);
        if (bolsa == null) { Falta(N_BOLSA); return; }
        bolsaEscala0 = bolsa.localScale;
        bolsaPos0 = bolsa.position;
        bolsaRot0 = bolsa.rotation;
        Bounds b = Caja(bolsa);
        bolsaTop0 = new Vector3(b.center.x, b.max.y, b.center.z);   // el cuello esta arriba
        // eje local mas vertical = el largo de la bolsa
        float mejor = -1f;
        for (int i = 0; i < 3; i++)
        {
            Vector3 ax = i == 0 ? Vector3.right : (i == 1 ? Vector3.up : Vector3.forward);
            Vector3 w = bolsa.TransformDirection(ax).normalized;
            float v = Mathf.Abs(Vector3.Dot(w, Vector3.up));
            if (v > mejor) { mejor = v; bagEje = i; bagDirL0 = w; }
        }
    }

    // La bolsa se escala y se gira alrededor de su cuello (ancla)
    void ApplyBag(Vector3 ancla, Quaternion rel)
    {
        if (bolsa == null) return;
        Vector3 off0 = bolsaPos0 - bolsaTop0;
        float offL = Vector3.Dot(off0, bagDirL0);
        Vector3 off = (off0 - bagDirL0 * offL) * bagS + bagDirL0 * (offL * bagL);
        Vector3 esc = bolsaEscala0 * bagS;
        esc[bagEje] = bolsaEscala0[bagEje] * bagL;
        bolsa.localScale = esc;
        bolsa.rotation = rel * bolsaRot0;
        bolsa.position = ancla + rel * off;
    }

    // k = 0..1 -> bolsa vacia a llena (modo BAG)
    public void SetBolsa(float k)
    {
        bagS = bagL = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(k));
        if (oBolsa != null) ApplyBag(oBolsa.curRef, oBolsa.curRot);
    }

    // forma del pulmon de prueba: ancho casi fijo y largo que cambia (se estira hacia abajo al inflarse)
    public void SetBolsaForma(float ancho, float largo)
    {
        bagS = Mathf.Clamp(ancho, 0.4f, 1.3f);
        bagL = Mathf.Clamp(largo, 0.4f, 1.3f);
        if (oBolsa != null) ApplyBag(oBolsa.curRef, oBolsa.curRot);
    }

    // escala directa (uniforme)
    public void SetBolsaEscala(float s)
    {
        bagS = bagL = Mathf.Clamp(s, 0.4f, 1.15f);
        if (oBolsa != null) ApplyBag(oBolsa.curRef, oBolsa.curRot);
    }

    // ---------- Pieza Y y tapon ----------
    void CrearYTapon()
    {
        Transform c1 = buscar(N_CONECTOR_A), c2 = buscar(N_CONECTOR_B);
        if (c1 == null || c2 == null) { Falta(c1 == null ? N_CONECTOR_A : N_CONECTOR_B); return; }

        Vector3 A = Caja(c1).center, B = Caja(c2).center;
        U = Vector3.Distance(A, B) / 6.54f;   // una unidad del modelo en el mundo
        if (U < 1e-6f) U = 0.01f;

        Vector3 mc = Caja(raiz != null ? raiz : c1).center;
        // el frente de la maquina es hacia donde mira la esfera del manometro (en horizontal)
        Vector3 frente = Vector3.ProjectOnPlane(dialEje, Vector3.up);
        if (frente.sqrMagnitude < 1e-6f)
        {
            Transform fl = buscar(N_FLUSH);
            frente = fl != null ? Vector3.ProjectOnPlane(Caja(fl).center - mc, Vector3.up) : Vector3.zero;
        }
        if (frente.sqrMagnitude < 1e-12f) frente = raiz != null ? raiz.forward : Vector3.forward;
        frente.Normalize();
        Vector3 lat = Vector3.ProjectOnPlane((A + B) * 0.5f - mc, Vector3.up);
        lat -= Vector3.Dot(lat, frente) * frente;
        if (lat.sqrMagnitude < 1e-12f) lat = Vector3.Cross(Vector3.up, frente);
        lat.Normalize();
        yLado = lat;

        float a = giroSalidaY * Mathf.Deg2Rad;
        yOut = (frente * Mathf.Cos(a) + lat * Mathf.Sin(a)).normalized;

        Vector3 M = (A + B) * 0.5f;
        yUnion = M + yOut * (2.5f * U);
        yPuerto = yUnion + yOut * (largoSalidaY * U);

        GameObject go = new GameObject("PiezaY_Anestesia");
        yRoot = go.transform;
        Material plastico = Mat(new Color(0.72f, 0.86f, 0.92f), 0.8f);
        Material azul = Mat(new Color(0.1f, 0.25f, 0.8f), 0.5f);

        Tubo(yRoot, A, yUnion, 1.0f * U, plastico);
        Tubo(yRoot, B, yUnion, 1.0f * U, plastico);
        Tubo(yRoot, yUnion, yPuerto, 1.3f * U, plastico);
        Tubo(yRoot, yPuerto - yOut * (1.4f * U), yPuerto, 1.9f * U, azul);   // conector de 22 mm

        matCordon = Mat(new Color(0.08f, 0.08f, 0.08f), 0.2f);
        cordonTapon = Cordon("Cordon_Tapon");

        // ----- Codo donde cuelga el pulmon de prueba (solo se ve con la bolsa conectada) -----
        Vector3 codoP1 = yPuerto + yOut * (1.8f * U);
        codoFin = codoP1 + Vector3.down * (2.2f * U);
        GameObject gc = new GameObject("Codo_Pulmon");
        codo = gc.transform;
        codo.SetParent(yRoot, false);
        Tubo(codo, yPuerto - yOut * (1.2f * U), codoP1, 1.9f * U, azul);
        Tubo(codo, codoP1, codoFin, 1.9f * U, azul);
        GameObject esf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esf.name = "CodoEsfera";
        Destroy(esf.GetComponent<Collider>());
        esf.transform.SetParent(codo, true);
        esf.transform.position = codoP1;
        esf.transform.localScale = Vector3.one * (3.8f * U);
        esf.GetComponent<Renderer>().sharedMaterial = azul;
        gc.SetActive(false);

        // ----- Tapon de prueba (agarrable) -----
        GameObject gt = new GameObject("Tapon_Y");
        tapon = gt.transform;
        tapon.SetParent(yRoot, false);
        Material goma = Mat(new Color(0.07f, 0.07f, 0.08f), 0.25f);
        Material azulMango = Mat(new Color(0.12f, 0.3f, 0.85f), 0.45f);
        Material azulOscuro = Mat(new Color(0.06f, 0.14f, 0.5f), 0.4f);
        Parte(tapon, 0f, -0.9f, 2.1f, 1.8f, goma);        // vastago de goma que entra a la Y
        Parte(tapon, 0f, 0.25f, 4.2f, 0.5f, azulOscuro);  // brida que apoya en la Y
        Parte(tapon, 0f, 1.5f, 2.6f, 2.0f, azulMango);    // mango
        Parte(tapon, 0f, 1.0f, 3.0f, 0.25f, azulOscuro);  // aros del mango
        Parte(tapon, 0f, 1.5f, 3.0f, 0.25f, azulOscuro);
        Parte(tapon, 0f, 2.0f, 3.0f, 0.25f, azulOscuro);
        Parte(tapon, 0f, 2.55f, 2.2f, 0.2f, goma);        // tapa superior
        SphereCollider sc = gt.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.center = new Vector3(0f, 1.0f * U, 0f);
        sc.radius = 3.2f * U;

        oTapon = new Obj();
        oTapon.id = ID_TAPON;
        oTapon.t = tapon;
        oTapon.esBolsa = false;
        oTapon.restRef = yUnion + yOut * (reposoTapon.x * U) + yLado * (reposoTapon.y * U) + Vector3.up * (reposoTapon.z * U);
        oTapon.restRot = Quaternion.identity;                                  // mango hacia arriba, colgando
        oTapon.placedRef = yPuerto;
        oTapon.placedRot = Quaternion.FromToRotation(Vector3.up, yOut);          // mango hacia afuera
        oTapon.iman = 40f * U;
        oTapon.encaje = 16f * U;

        // ----- La bolsa del modelo: reposo en el brazo, "puesta" = en la Y -----
        if (bolsa != null)
        {
            oBolsa = new Obj();
            oBolsa.id = ID_BOLSA;
            oBolsa.t = bolsa;
            oBolsa.esBolsa = true;
            oBolsa.restRef = bolsaTop0;
            oBolsa.restRot = Quaternion.identity;
            // el pulmon cuelga hacia abajo del codo
            oBolsa.placedRef = codoFin + Vector3.up * (0.8f * U);
            oBolsa.placedRot = Quaternion.identity;
            oBolsa.iman = 50f * U;
            oBolsa.encaje = 22f * U;
        }

        // zona de destino
        ghost = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ghost.name = "ZonaDestino";
        Destroy(ghost.GetComponent<Collider>());
        Shader sh = Shader.Find("Sprites/Default");
        matGhost = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Unlit"));
        matGhost.color = new Color(0.1f, 1f, 0.3f, 0.35f);
        ghost.GetComponent<Renderer>().sharedMaterial = matGhost;
        ghost.SetActive(false);
    }

    // Cilindro con eje vertical local, centrado en y (en unidades U) con diametro d y alto h
    void Parte(Transform padre, float x, float y, float d, float h, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = "Parte";
        Destroy(g.GetComponent<Collider>());
        Transform t = g.transform;
        t.SetParent(padre, false);
        t.localPosition = new Vector3(x * U, y * U, 0f);
        t.localScale = new Vector3(d * U, h * U * 0.5f, d * U);
        g.GetComponent<Renderer>().sharedMaterial = m;
    }

    Material Mat(Color c, float suavidad)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        Material m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", suavidad);
        return m;
    }

    void Tubo(Transform padre, Vector3 de, Vector3 a, float radio, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = "TuboY";
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

    LineRenderer Cordon(string nombre)
    {
        GameObject g = new GameObject(nombre);
        g.transform.SetParent(yRoot, false);
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.widthMultiplier = 0.25f * U;
        lr.sharedMaterial = matCordon;
        lr.numCapVertices = 3;
        return lr;
    }

    // ================= Agarrar y colocar =================
    Obj Get(string id)
    {
        if (oTapon != null && id == oTapon.id) return oTapon;
        if (oBolsa != null && id == oBolsa.id) return oBolsa;
        return null;
    }

    void Aplicar(Obj o, Vector3 r, Quaternion q)
    {
        o.curRef = r; o.curRot = q;
        if (o.esBolsa) ApplyBag(r, q);
        else { o.t.position = r; o.t.rotation = q; }
    }

    void Colliders(Obj o, bool on)
    {
        Collider[] cs = o.t.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cs.Length; i++) cs[i].enabled = on;
    }

    public bool PuedeAgarrar(string id) { return Get(id) != null; }

    // Donde debe terminar el objeto si se suelta con exito
    public Vector3 DestinoRef(string id) { Obj o = Get(id); return o == null ? Vector3.zero : (o.puesto ? o.restRef : o.placedRef); }
    public float RadioIman(string id) { Obj o = Get(id); return o == null ? 0f : o.iman; }
    public float RadioEncaje(string id) { Obj o = Get(id); return o == null ? 0f : o.encaje; }
    public Vector3 PosRef(string id) { Obj o = Get(id); return o == null ? Vector3.zero : o.curRef; }
    public bool EstaPuesto(string id) { Obj o = Get(id); return o != null && o.puesto; }

    // Un solo toque: la pieza se pone en su destino (o regresa a su lugar) con una animacion corta
    public void Colocar(string id)
    {
        Obj o = Get(id);
        if (o == null) return;
        if (o.co != null) { StopCoroutine(o.co); o.co = null; }
        Colliders(o, false);
        if (o.esBolsa && o.puesto && codo != null) codo.gameObject.SetActive(false);
        bool puestoFin = !o.puesto;
        Vector3 refFin = puestoFin ? o.placedRef : o.restRef;
        Quaternion rotFin = puestoFin ? o.placedRot : o.restRot;
        o.co = StartCoroutine(IrA(o, refFin, rotFin, puestoFin, 0.8f, 4f * U));
    }

    public void IniciarAgarre(string id)
    {
        Obj o = Get(id);
        if (o == null) return;
        if (o.co != null) { StopCoroutine(o.co); o.co = null; }
        Colliders(o, false);
        MostrarDestino(DestinoRef(id), o.encaje);
    }

    // La mano lleva el objeto; cerca del destino se orienta solo (w = 0..1)
    public void MoverAMano(string id, Vector3 mano, float w)
    {
        Obj o = Get(id);
        if (o == null) return;
        Quaternion r0 = o.puesto ? o.placedRot : o.restRot;
        Quaternion r1 = o.puesto ? o.restRot : o.placedRot;
        Aplicar(o, mano, Quaternion.Slerp(r0, r1, w));
    }

    // exito = true: queda en su destino; false: regresa a donde estaba
    public void Soltar(string id, bool exito)
    {
        Obj o = Get(id);
        if (o == null) return;
        OcultarDestino();
        Vector3 refFin; Quaternion rotFin; bool puestoFin;
        if (exito)
        {
            puestoFin = !o.puesto;
            refFin = puestoFin ? o.placedRef : o.restRef;
            rotFin = puestoFin ? o.placedRot : o.restRot;
        }
        else
        {
            puestoFin = o.puesto;
            refFin = puestoFin ? o.placedRef : o.restRef;
            rotFin = puestoFin ? o.placedRot : o.restRot;
        }
        if (o.co != null) StopCoroutine(o.co);
        o.co = StartCoroutine(IrA(o, refFin, rotFin, puestoFin, 0.4f, 0f));
    }

    IEnumerator IrA(Obj o, Vector3 refFin, Quaternion rotFin, bool puestoFin, float dur, float arco)
    {
        Vector3 r0 = o.curRef; Quaternion q0 = o.curRot;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            Aplicar(o, Vector3.Lerp(r0, refFin, k) + Vector3.up * (arco * Mathf.Sin(Mathf.PI * k)), Quaternion.Slerp(q0, rotFin, k));
            yield return null;
        }
        Aplicar(o, refFin, rotFin);
        o.puesto = puestoFin;
        if (o.esBolsa && codo != null) codo.gameObject.SetActive(puestoFin);
        Colliders(o, true);
        o.co = null;
    }

    void MostrarDestino(Vector3 pos, float radio)
    {
        if (ghost == null) return;
        ghost.transform.position = pos;
        ghostRadio = radio * 0.55f;
        ghost.transform.localScale = Vector3.one * ghostRadio * 2f;
        ghost.SetActive(true);
    }

    public void OcultarDestino() { if (ghost != null) ghost.SetActive(false); }

    void LateUpdate()
    {
        if (ghost != null && ghost.activeSelf)
        {
            float p = 1f + 0.12f * Mathf.Sin(Time.time * 6f);
            ghost.transform.localScale = Vector3.one * ghostRadio * 2f * p;
        }
        if (tapon != null && cordonTapon != null)
        {
            cordonTapon.SetPosition(0, yUnion);
            cordonTapon.SetPosition(1, tapon.position + tapon.up * (2.6f * U));
        }
    }

    // ================= Reinicio =================
    public void Reiniciar()
    {
        StopAllCoroutines();
        if (gO2 != null) { gO2.co = null; AplicarAng(gO2, 0f); }
        if (gAPL != null) { gAPL.co = null; AplicarAng(gAPL, 0f); }
        if (gPal != null) { gPal.co = null; AplicarAng(gPal, 0f); }
        OcultarDestino();
        SetAguja(0f);
        SetFlotador(0f);
        SetFuelle(0.1f);
        bagS = bagL = 0.55f;
        if (codo != null) codo.gameObject.SetActive(false);
        if (oTapon != null) { oTapon.puesto = false; oTapon.co = null; Aplicar(oTapon, oTapon.restRef, oTapon.restRot); Colliders(oTapon, true); }
        if (oBolsa != null) { oBolsa.puesto = false; oBolsa.co = null; Aplicar(oBolsa, oBolsa.restRef, oBolsa.restRot); Colliders(oBolsa, true); }
        else SetBolsa(0f);
    }
}
