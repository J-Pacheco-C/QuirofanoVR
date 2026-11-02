using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Dibuja por codigo la pantalla del equipo (sin sprites).
// Los tamanos de letra se calculan a partir del alto real de la pantalla.
public class PantallaEquipo
{
    static readonly string[] NombresEtapas =
    {
        "Autocomprobacion de encendido",
        "Lista de comprobacion preoperatoria",
        "Prueba automatica de fugas",
        "Prueba del ventilador"
    };

    static readonly Color Cian = new Color(0.35f, 0.85f, 1f);
    static readonly Color Verde = new Color(0.2f, 0.9f, 0.3f);
    static readonly Color Rojo = new Color(1f, 0.3f, 0.25f);
    static readonly Color Gris = new Color(0.65f, 0.7f, 0.75f);

    float H = 100f; // alto de la pantalla en unidades del canvas

    GameObject gStandby, gCheck, gLista, gPrueba;
    TMP_Text txtTitulo, txtReloj, txtItems, txtPrueba, txtDetalles, txtAprob, txtPawValor, txtFooter;
    TMP_Text[] txtEtapas = new TMP_Text[4];
    RectTransform barFill, pawFill;
    Image pawFillImg;
    float ultimoReloj = -100f;

    public PantallaEquipo(RectTransform parent)
    {
        float h = parent.rect.height;
        if (h < 1f)
        {
            Canvas cv = parent.GetComponentInParent<Canvas>();
            if (cv != null) h = ((RectTransform)cv.transform).rect.height;
        }
        if (h >= 1f) H = h;

        RectTransform raiz = Rect(parent, "PantallaUI", Vector2.zero, Vector2.one);

        // ---- Standby ----
        RectTransform rs = Rect(raiz, "Standby", Vector2.zero, Vector2.one);
        gStandby = rs.gameObject;
        Txt(rs, "Texto", new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.75f), "Standby", Color.white, TextAlignmentOptions.Center, 0.16f);
        Txt(rs, "Hint", new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.4f),
            "Presione la perilla principal para iniciar la comprobacion del sistema", Gris, TextAlignmentOptions.Center, 0.05f);

        // ---- Comprobacion del sistema ----
        RectTransform rc = Rect(raiz, "Comprobacion", Vector2.zero, Vector2.one);
        gCheck = rc.gameObject;
        Panel(rc, "Header", new Vector2(0f, 0.9f), new Vector2(1f, 1f), new Color(0.05f, 0.12f, 0.22f));
        txtTitulo = Txt(rc, "Titulo", new Vector2(0.02f, 0.9f), new Vector2(0.68f, 1f), "Comprobacion del sistema", Color.white, TextAlignmentOptions.Left, 0.055f);
        txtReloj = Txt(rc, "Reloj", new Vector2(0.68f, 0.9f), new Vector2(0.98f, 1f), "", Gris, TextAlignmentOptions.Right, 0.04f);

        Panel(rc, "PanelEtapas", new Vector2(0f, 0.12f), new Vector2(0.29f, 0.9f), new Color(0.03f, 0.05f, 0.09f));
        for (int i = 0; i < 4; i++)
        {
            float yMax = 0.88f - i * 0.19f;
            txtEtapas[i] = Txt(rc, "Etapa" + i, new Vector2(0.015f, yMax - 0.17f), new Vector2(0.285f, yMax), NombresEtapas[i], Gris, TextAlignmentOptions.Left, 0.036f);
        }

        txtFooter = Txt(rc, "Footer", new Vector2(0.02f, 0.01f), new Vector2(0.76f, 0.11f), "", Gris, TextAlignmentOptions.Left, 0.034f);

        // Panel de presion de vias aereas (Paw)
        Image pawBg = Panel(rc, "PawFondo", new Vector2(0.82f, 0.2f), new Vector2(0.94f, 0.8f), new Color(0.1f, 0.12f, 0.16f));
        pawFillImg = Panel(pawBg.rectTransform, "PawBarra", Vector2.zero, new Vector2(1f, 0f), Verde);
        pawFill = pawFillImg.rectTransform;
        Txt(rc, "PawTitulo", new Vector2(0.78f, 0.81f), new Vector2(0.98f, 0.89f), "Paw", Cian, TextAlignmentOptions.Center, 0.045f);
        txtPawValor = Txt(rc, "PawValor", new Vector2(0.76f, 0.1f), new Vector2(1f, 0.19f), "0 cmH2O", Color.white, TextAlignmentOptions.Center, 0.036f);

        // Pagina: lista de instrucciones
        RectTransform rl = Rect(rc, "Lista", new Vector2(0.31f, 0.14f), new Vector2(0.78f, 0.88f));
        gLista = rl.gameObject;
        txtItems = Txt(rl, "Items", Vector2.zero, Vector2.one, "", Color.white, TextAlignmentOptions.TopLeft, 0.042f);

        // Pagina: prueba en curso
        RectTransform rp = Rect(rc, "Prueba", new Vector2(0.31f, 0.14f), new Vector2(0.78f, 0.88f));
        gPrueba = rp.gameObject;
        txtPrueba = Txt(rp, "Etiqueta", new Vector2(0f, 0.82f), new Vector2(1f, 1f), "", Color.white, TextAlignmentOptions.Left, 0.05f);
        Image barBg = Panel(rp, "BarraFondo", new Vector2(0f, 0.68f), new Vector2(1f, 0.79f), new Color(0.15f, 0.17f, 0.2f));
        barFill = Panel(barBg.rectTransform, "BarraRelleno", Vector2.zero, new Vector2(0f, 1f), Verde).rectTransform;
        txtDetalles = Txt(rp, "Detalles", new Vector2(0f, 0.25f), new Vector2(1f, 0.62f), "", Color.white, TextAlignmentOptions.TopLeft, 0.04f);
        txtAprob = Txt(rp, "Aprob", new Vector2(0f, 0f), new Vector2(1f, 0.24f), "", Verde, TextAlignmentOptions.Center, 0.1f);

        Apagada();
    }

    // ---------- Vistas ----------
    public void Apagada() { gStandby.SetActive(false); gCheck.SetActive(false); }

    public void Standby() { gStandby.SetActive(true); gCheck.SetActive(false); }

    void ModoCheck()
    {
        gStandby.SetActive(false);
        gCheck.SetActive(true);
        Reloj();
    }

    public void Lista(int[] etapas, string[] items, bool[] hechos)
    {
        ModoCheck();
        gLista.SetActive(true);
        gPrueba.SetActive(false);
        txtTitulo.text = "Comprobacion del sistema";
        Etapas(etapas);
        System.Text.StringBuilder sb = new System.Text.StringBuilder("Instrucciones:\n");
        for (int i = 0; i < items.Length; i++)
        {
            sb.Append(hechos[i] ? "<color=#33E64D>[OK]</color> " : "[  ] ");
            sb.Append(items[i]).Append('\n');
        }
        txtItems.text = sb.ToString();
        txtFooter.text = "Siga las instrucciones para continuar.";
    }

    public void Prueba(int[] etapas, string titulo, string etiqueta, float frac, string detalles, bool aprob)
    {
        ModoCheck();
        gLista.SetActive(false);
        gPrueba.SetActive(true);
        txtTitulo.text = titulo;
        Etapas(etapas);
        txtPrueba.text = etiqueta;
        barFill.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
        txtDetalles.text = detalles;
        txtAprob.text = aprob ? "Aprob" : "";
        txtFooter.text = aprob ? "Prueba completada." : "No toque la maquina durante la prueba.";
    }

    public void Paw(float v)
    {
        v = Mathf.Max(0f, v);
        txtPawValor.text = Mathf.RoundToInt(v) + " cmH2O";
        pawFill.anchorMax = new Vector2(1f, Mathf.Clamp01(v / 40f));
        pawFillImg.color = v > 35f ? Rojo : Verde;
    }

    public void Tick()
    {
        if (gCheck.activeSelf && Time.time - ultimoReloj > 5f) Reloj();
    }

    // ---------- Internos ----------
    void Reloj()
    {
        ultimoReloj = Time.time;
        txtReloj.text = System.DateTime.Now.ToString("yyyy/MM/dd HH:mm");
    }

    // estados: 0 pendiente, 1 activa, 2 hecha
    void Etapas(int[] e)
    {
        for (int i = 0; i < 4; i++)
        {
            string pref = e[i] == 2 ? "[OK] " : (e[i] == 1 ? "> " : "");
            txtEtapas[i].text = pref + NombresEtapas[i];
            txtEtapas[i].color = e[i] == 2 ? Verde : (e[i] == 1 ? Cian : Gris);
        }
    }

    static RectTransform Rect(Transform parent, string nombre, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        return rt;
    }

    static Image Panel(Transform parent, string nombre, Vector2 min, Vector2 max, Color c)
    {
        RectTransform rt = Rect(parent, nombre, min, max);
        Image im = rt.gameObject.AddComponent<Image>();
        im.color = c;
        im.raycastTarget = false;
        return im;
    }

    // f = tamano de letra como fraccion del alto de la pantalla
    TMP_Text Txt(Transform parent, string nombre, Vector2 min, Vector2 max, string s, Color c, TextAlignmentOptions al, float f)
    {
        RectTransform rt = Rect(parent, nombre, min, max);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.enableAutoSizing = false;
        t.fontSize = H * f;
        t.text = s;
        t.color = c;
        t.alignment = al;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }
}
