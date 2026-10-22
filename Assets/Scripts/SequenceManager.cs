using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public enum TipoAccion { Presionar, Mantener }
public enum PantallaSprite { Ninguno, Standby, Menu, Chequeo, Okay }

[System.Serializable]
public class PasoRutina
{
    public string nombre;
    public string piezaId;                 // nombre exacto de la pieza
    public TipoAccion accion;              // Presionar (un toque) o Mantener (mantener E)
    [TextArea] public string instruccion;  // texto que ve el alumno
    public PantallaSprite spriteAlAccionar;
    public bool girarRueda;                // gira Stickers.014
    public bool mover;                     // mueve Parts1.051
    public Vector3 destinoLocal;
    public float velocidad = 0.02f;
    public float segundosEspera;           // espera despues de la accion (ej. chequeo 30 s)
    public PantallaSprite spriteAlFinalizar;
}

public class SequenceManager : MonoBehaviour
{
    public static SequenceManager Instance;

    [Header("Pieza que se mueve (Parts1.051)")]
    public Transform parteMovil;

    [Header("Rueda reguladora (Stickers.014)")]
    public Transform rueda;
    public Vector3 ejeRueda = new Vector3(0, 0, 1);
    public float gradosRueda = 90f;

    [Header("Pantalla del equipo")]
    public Image pantallaImage;
    public Sprite spriteStandby;
    public Sprite spriteMenu;
    public Sprite spriteChequeoProgreso;
    public Sprite spriteOkay;

    [Header("Textos en pantalla (opcional: si se dejan vacios se crean solos)")]
    public TMP_Text textoInstruccion;
    public TMP_Text textoMensaje;

    [Header("Rutina (si esta vacia se carga la rutina por defecto)")]
    public List<PasoRutina> pasos = new List<PasoRutina>();

    private int idx = 0;
    private bool ocupado = false;          // esperando movimiento o temporizador
    private bool esperandoMovimiento = false;
    private bool esperandoTiempo = false;
    private float tiempoEspera = 0f;
    private float gradosPendientes = 0f;
    private bool terminado = false;
    private bool iniciada = false;
    private float tiempoInicio = 0f;
    private int errores = 0;
    private float mensajeTimer = 0f;
    private Quaternion ruedaRotInicial;
    private Vector3 parteInicial = new Vector3(0f, -0.114f, 0f);

    void Awake() { Instance = this; }

    void Start()
    {
        if (pasos == null || pasos.Count == 0) CargarRutinaPorDefecto();
        if (textoInstruccion == null || textoMensaje == null) CrearHUD();
        if (rueda != null) ruedaRotInicial = rueda.localRotation;
        Reiniciar();
    }

    [ContextMenu("Cargar rutina por defecto")]
    public void CargarRutinaPorDefecto()
    {
        pasos = new List<PasoRutina>
        {
            new PasoRutina { nombre="Menu", piezaId="Parts1.035", accion=TipoAccion.Presionar,
                instruccion="Paso 1: Presiona la perilla (Parts1.035) para abrir el menu.",
                spriteAlAccionar=PantallaSprite.Menu },
            new PasoRutina { nombre="Rueda", piezaId="Stickers.014", accion=TipoAccion.Presionar,
                instruccion="Paso 2: Selecciona la rueda reguladora (Stickers.014).",
                girarRueda=true },
            new PasoRutina { nombre="O2", piezaId="Stickers.018", accion=TipoAccion.Mantener,
                instruccion="Paso 3: Manten presionado el boton O2 (Stickers.018) hasta que el indicador suba por completo.",
                mover=true, destinoLocal=new Vector3(0f,0f,0f) },
            new PasoRutina { nombre="Chequeo 1", piezaId="Parts1.035", accion=TipoAccion.Presionar,
                instruccion="Paso 4: Presiona la perilla (Parts1.035) para iniciar el chequeo.",
                spriteAlAccionar=PantallaSprite.Chequeo, segundosEspera=30f, spriteAlFinalizar=PantallaSprite.Okay },
            new PasoRutina { nombre="Ventilacion", piezaId="Stickers.014", accion=TipoAccion.Presionar,
                instruccion="Paso 5: Segundo chequeo (ventilacion mecanica). Presiona la rueda (Stickers.014).",
                girarRueda=true, mover=true, destinoLocal=new Vector3(0f,-0.1357f,0f) },
            new PasoRutina { nombre="Boton vent.", piezaId="Parts1.023", accion=TipoAccion.Mantener,
                instruccion="Paso 6: Manten presionado el boton (Parts1.023) hasta que el indicador suba.",
                mover=true, destinoLocal=new Vector3(0f,0.0178f,0f) },
            new PasoRutina { nombre="Chequeo 2", piezaId="Parts1.035", accion=TipoAccion.Presionar,
                instruccion="Paso 7: Presiona la perilla (Parts1.035) para iniciar el segundo chequeo.",
                spriteAlAccionar=PantallaSprite.Chequeo, segundosEspera=30f, spriteAlFinalizar=PantallaSprite.Okay },
        };
    }

    // ---------- HUD ----------
    void CrearHUD()
    {
        var cgo = new GameObject("HUD_Rutina");
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        if (textoInstruccion == null)
            textoInstruccion = CrearTexto(cgo.transform, "Instruccion", new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(1500, 130), 38, Color.white, true);
        if (textoMensaje == null)
            textoMensaje = CrearTexto(cgo.transform, "Mensaje", new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(1500, 90), 36, Color.yellow, false);
    }

    TMP_Text CrearTexto(Transform padre, string nombre, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, Color color, bool conFondo)
    {
        // Contenedor (con fondo opcional). Image y TextMeshPro no pueden ir en el mismo objeto.
        var cont = new GameObject(nombre + "_Fondo");
        cont.transform.SetParent(padre, false);
        var rt = cont.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        if (conFondo)
        {
            var img = cont.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.6f);
            img.raycastTarget = false;
        }

        var go = new GameObject(nombre);
        go.transform.SetParent(cont.transform, false);
        var trt = go.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(10, 5);
        trt.offsetMax = new Vector2(-10, -5);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    // ---------- Flujo ----------
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) Reiniciar();

        if (esperandoTiempo && !terminado)
        {
            tiempoEspera += Time.deltaTime;
            var paso = pasos[idx];
            float restante = Mathf.Max(0f, paso.segundosEspera - tiempoEspera);
            SetInstruccion("Chequeo en progreso... " + Mathf.CeilToInt(restante) + " s");
            if (tiempoEspera >= paso.segundosEspera)
            {
                esperandoTiempo = false;
                ocupado = false;
                MostrarSprite(paso.spriteAlFinalizar);
                Mensaje("Chequeo completado correctamente.", Color.green);
                Avanzar();
            }
        }

        if (gradosPendientes > 0f && rueda != null)
        {
            float p = Mathf.Min(180f * Time.deltaTime, gradosPendientes);
            rueda.Rotate(ejeRueda, p, Space.Self);
            gradosPendientes -= p;
        }

        if (mensajeTimer > 0f)
        {
            mensajeTimer -= Time.deltaTime;
            if (mensajeTimer <= 0f && textoMensaje != null) textoMensaje.text = "";
        }
    }

    public void Reiniciar()
    {
        idx = 0; ocupado = false; esperandoMovimiento = false; esperandoTiempo = false;
        tiempoEspera = 0f; gradosPendientes = 0f; terminado = false; iniciada = false; errores = 0;
        MostrarSprite(PantallaSprite.Standby);
        if (parteMovil != null) parteMovil.localPosition = parteInicial;
        if (rueda != null) rueda.localRotation = ruedaRotInicial;
        if (textoMensaje != null) textoMensaje.text = "";
        SetInstruccion(pasos[0].instruccion);
        Debug.Log("[Rutina] Reiniciada. Presiona R en cualquier momento para reiniciar.");
    }

    void Avanzar()
    {
        idx++;
        if (idx >= pasos.Count) { Terminar(); return; }
        SetInstruccion(pasos[idx].instruccion);
    }

    void Terminar()
    {
        terminado = true;
        float t = Time.time - tiempoInicio;
        int min = (int)(t / 60f); int seg = (int)(t % 60f);
        SetInstruccion("Rutina completada!\nTiempo: " + min + ":" + seg.ToString("00") + "   Errores: " + errores + "\n(R para reiniciar)");
        Mensaje("", Color.white);
        Debug.Log("[Rutina] Terminada. Tiempo " + t.ToString("F1") + " s, errores " + errores);
    }

    public void OnPiezaPresionada(string pieza)
    {
        if (terminado || ocupado) return;
        var paso = pasos[idx];

        if (pieza != paso.piezaId)
        {
            errores++;
            Mensaje("Esa pieza no corresponde a este paso. Errores: " + errores, Color.red);
            Debug.Log("[Rutina] Error: se toco " + pieza + " pero se esperaba " + paso.piezaId);
            return;
        }
        if (paso.accion == TipoAccion.Mantener) return; // este paso se completa manteniendo E

        IniciarCronometro();
        MostrarSprite(paso.spriteAlAccionar);
        if (paso.girarRueda) gradosPendientes += gradosRueda;

        if (paso.mover && parteMovil != null)
        {
            ocupado = true; esperandoMovimiento = true;
            parteMovil.GetComponent<MovablePart>().MoveTowards(paso.destinoLocal, paso.velocidad);
        }
        else FinalizarAccion();
    }

    // Cada frame mientras se mantiene E sobre la pieza
    public void OnBotonMantenido(string pieza)
    {
        if (terminado || ocupado) return;
        var paso = pasos[idx];
        if (paso.accion != TipoAccion.Mantener || pieza != paso.piezaId || parteMovil == null) return;

        IniciarCronometro();
        var mp = parteMovil.GetComponent<MovablePart>();
        if (mp.Step(paso.destinoLocal, paso.velocidad))
        {
            MostrarSprite(paso.spriteAlAccionar);
            FinalizarAccion();
        }
    }

    // Termina de moverse la pieza (movimiento automatico)
    public void OnPartReachedTarget()
    {
        if (esperandoMovimiento)
        {
            esperandoMovimiento = false;
            ocupado = false;
            FinalizarAccion();
        }
    }

    void FinalizarAccion()
    {
        var paso = pasos[idx];
        if (paso.segundosEspera > 0f)
        {
            ocupado = true; esperandoTiempo = true; tiempoEspera = 0f;
        }
        else Avanzar();
    }

    void IniciarCronometro()
    {
        if (iniciada) return;
        iniciada = true; tiempoInicio = Time.time;
    }

    public bool EsPiezaActual(string pieza)
    {
        if (terminado || ocupado || pasos == null || idx >= pasos.Count) return false;
        return pasos[idx].piezaId == pieza;
    }

    // ---------- Utilidades ----------
    void MostrarSprite(PantallaSprite s)
    {
        if (pantallaImage == null) return;
        Sprite sp = null;
        switch (s)
        {
            case PantallaSprite.Standby: sp = spriteStandby; break;
            case PantallaSprite.Menu: sp = spriteMenu; break;
            case PantallaSprite.Chequeo: sp = spriteChequeoProgreso; break;
            case PantallaSprite.Okay: sp = spriteOkay; break;
        }
        if (sp != null) pantallaImage.sprite = sp;
    }

    void SetInstruccion(string t) { if (textoInstruccion != null) textoInstruccion.text = t; }

    void Mensaje(string t, Color c)
    {
        if (textoMensaje == null) return;
        textoMensaje.text = t; textoMensaje.color = c; mensajeTimer = 3f;
    }
}
