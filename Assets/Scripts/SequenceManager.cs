using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum TipoAccion { Presionar, Mantener, Esperar, Agarrar }

[System.Serializable]
public class PasoRutina
{
    public string id;                      // identifica el efecto del paso
    public string piezaId;                 // nombre exacto de la pieza (vacio si es Esperar)
    public TipoAccion accion;
    [TextArea] public string instruccion;  // lo que debe hacer el alumno
    [TextArea] public string porQue;       // por que se hace
    [TextArea] public string textoEspera;  // texto mientras se espera
    public float segundos;                 // Presionar/Esperar: espera tras la accion
}

// Rutina guiada de comprobacion previa al uso (prueba de fugas del circuito respiratorio,
// modo BAG y modo VENT, y prueba del ventilador con pulmon de prueba).
// Las piezas que se agarran (tapon de la Y, bolsa) se llevan manteniendo E y se sueltan en la zona verde.
public class SequenceManager : MonoBehaviour
{
    public static SequenceManager Instance;

    [Header("Maquina (si se deja vacio busca 'anestesia2')")]
    public Transform raizMaquina;
    [Tooltip("Ya no se usa (el fuelle se mueve desde CircuitoVisual)")]
    public Transform parteMovil;

    [Header("Pantalla del equipo (el Image del Canvas de la pantalla)")]
    public Image pantallaImage;
    [Tooltip("Apagado = usa los sprites (recomendado). Encendido = dibuja la pantalla con texto por codigo")]
    public bool pantallaPorCodigo = false;
    public Sprite spriteStandby;
    public Sprite spriteMenu;
    public Sprite spriteChequeoProgreso;
    public Sprite spriteOkay;

    [Header("Textos en pantalla (opcional: si se dejan vacios se crean solos)")]
    public TMP_Text textoInstruccion;
    public TMP_Text textoMensaje;

    [Header("Piezas que se ponen")]
    [Tooltip("Apagado = presionas E sobre el tapon o la bolsa y se ponen solos (recomendado). Encendido = hay que mantener E, llevarlos a la zona verde y soltar (modo VR)")]
    public bool agarrarPiezas = false;

    [Header("Rutina")]
    [Tooltip("Si esta activo siempre se usa la rutina escrita en el codigo")]
    public bool usarRutinaPorDefecto = true;
    public List<PasoRutina> pasos = new List<PasoRutina>();

    // Piezas del modelo
    const string P_PRINCIPAL = "Parts1.035";
    const string P_O2 = CircuitoVisual.N_PERILLA_O2;
    const string P_APL = CircuitoVisual.N_PERILLA_APL;
    const string P_FLUSH = CircuitoVisual.N_FLUSH;
    const string P_VENT = "Parts1.023";
    const string P_SEL = CircuitoVisual.N_PALANCA;
    const string P_TAPON = CircuitoVisual.ID_TAPON;
    const string P_BOLSA = CircuitoVisual.ID_BOLSA;

    // Fuelle (concertina): k = 0..1 (1 = lleno, hasta arriba)
    const float BELL_VACIO = 0.1f;
    const float BELL_LLENO = 1f;
    const float BELL_VT_K = 0.3f;    // 500 mL de 1500 mL del fuelle = un tercio del recorrido

    // Bolsa usada como pulmon de prueba: ancho casi fijo; al inflarse se estira hacia abajo
    const float BOLSA_VACIA = 0.55f;
    const float BOLSA_ANCHO0 = 1.0f, BOLSA_ANCHO1 = 1.1f;
    const float BOLSA_LARGO0 = 0.72f, BOLSA_LARGO1 = 1.0f;

    // Ventilador de prueba: 12 resp/min (5 s), I:E 1:2
    const float VENT_T = 5f;
    const float VENT_TI = 1.67f;
    const float VENT_PEEP = 3f;
    const float VENT_PICO = 15f;

    static readonly Dictionary<string, string> Amigables = new Dictionary<string, string>
    {
        { P_PRINCIPAL, "Perilla principal" },
        { P_O2, "Perilla de flujo de O2" },
        { P_APL, "Perilla de la valvula APL" },
        { P_FLUSH, "Boton de O2 rapido" },
        { P_VENT, "Boton de ventilacion" },
        { P_SEL, "Palanca Bag/Vent" },
        { P_TAPON, "Tapon de la pieza Y" },
        { P_BOLSA, "Bolsa (pulmon de prueba)" },
    };

    static readonly string[] ItemsLista =
    {
        "Flujometro de O2 (flotador sube y baja)",
        "Valvula APL cerrada y pieza Y tapada",
        "Modo BAG: 30 cmH2O sin caida por 10 s",
        "Modo VENT: fuelle sin fugas"
    };

    // Estado de la rutina
    int idx = 0;
    bool ocupado = false;
    bool terminado = false;
    bool iniciada = false;
    float tiempoInicio = 0f;
    int errores = 0;
    float mensajeTimer = 0f;
    float esperaRestante = 0f;
    float esperaTotal = 1f;
    float hold = 0f;
    float presion = 0f;      // cmH2O en el circuito
    float presionBase = 0f;
    bool[] chk = new bool[4];
    bool enVistaLista = false;
    bool sosteniendo = false;
    float ultimoHold = 0f;
    bool ventilando = false;
    float ventT0 = 0f;
    float volVent = 0f;      // 0..1 fraccion del volumen corriente
    bool yTapada = false;

    // Piezas que se llevan en la mano
    bool llevando = false;
    string idLlevado = "";
    Vector3 manoPos;
    float distMano = 0.7f;
    Camera camara;

    // Piezas
    Transform tPrincipal, tO2, tAPL, tFlush, tVent, tSel;
    CircuitoVisual cv;
    PantallaEquipo pantalla;
    GameObject cajaMensaje;

    void Awake() { Instance = this; }

    void Start()
    {
        if (usarRutinaPorDefecto || pasos == null || pasos.Count == 0) CargarRutinaPorDefecto();
        AplicarTextosDeAgarre();
        if (textoInstruccion == null || textoMensaje == null) CrearHUD();
        CargarSpritesPorDefecto();
        BuscarPiezas();
        if (pantallaImage == null)
            Debug.LogWarning("[Rutina] No hay Pantalla Image asignado: la pantalla del equipo no se muestra. Arrastra el Image del Canvas de la pantalla al campo Pantalla Image del SequenceManager.");
        else if (pantallaPorCodigo)
        {
            pantallaImage.sprite = null;
            pantallaImage.color = new Color(0.01f, 0.01f, 0.02f, 1f);
            pantalla = new PantallaEquipo(pantallaImage.rectTransform);
        }
        Debug.Log("[Rutina] Pantalla: " + (pantallaImage == null ? "SIN Image" : pantallaImage.name) +
                  " | sprites Standby=" + (spriteStandby != null) + " Menu=" + (spriteMenu != null) +
                  " Progreso=" + (spriteChequeoProgreso != null) + " Okay=" + (spriteOkay != null));
        Reiniciar();
    }

    // Si los sprites se perdieron (por ejemplo tras recompilar), los busca solos en el Editor
    void CargarSpritesPorDefecto()
    {
#if UNITY_EDITOR
        const string ruta = "Assets/Starter Assets/Runtime/Mobile/UI/Images/";
        if (spriteStandby == null) spriteStandby = CargarSprite(ruta + "Standy.png", "Standy");
        if (spriteMenu == null) spriteMenu = CargarSprite(ruta + "Menu.png", "Menu");
        if (spriteChequeoProgreso == null) spriteChequeoProgreso = CargarSprite(ruta + "Progreso.png", "Progreso");
        if (spriteOkay == null) spriteOkay = CargarSprite(ruta + "Correcto.png", "Correcto");
#endif
        if (!pantallaPorCodigo && (spriteStandby == null || spriteMenu == null || spriteChequeoProgreso == null || spriteOkay == null))
            Debug.LogWarning("[Rutina] Faltan sprites de la pantalla. Arrastra Standy, Menu, Progreso y Correcto (Assets/Starter Assets/Runtime/Mobile/UI/Images) a los campos del SequenceManager.");
    }

#if UNITY_EDITOR
    static Sprite CargarSprite(string ruta, string nombre)
    {
        Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        if (s != null) return s;
        string[] guids = UnityEditor.AssetDatabase.FindAssets(nombre + " t:Sprite");
        for (int i = 0; i < guids.Length; i++)
        {
            string p = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == nombre)
            {
                s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (s != null) return s;
            }
        }
        return null;
    }
#endif

    // ================= Rutina =================
    [ContextMenu("Cargar rutina por defecto")]
    public void CargarRutinaPorDefecto()
    {
        pasos = new List<PasoRutina>
        {
            // ---- Encendido ----
            new PasoRutina { id="encender", piezaId=P_PRINCIPAL, accion=TipoAccion.Presionar, segundos=2f,
                instruccion="Paso 1: La maquina esta apagada. Presiona la Perilla principal para encenderla.",
                porQue="Antes de usar una maquina de anestesia se enciende y se espera a que arranque. Si la pantalla no responde, el equipo no debe usarse con un paciente.",
                textoEspera="Encendiendo la maquina..." },
            new PasoRutina { id="iniciar", piezaId=P_PRINCIPAL, accion=TipoAccion.Presionar,
                instruccion="Paso 2: La maquina esta en Standby. Presiona la Perilla principal para iniciar la comprobacion del sistema.",
                porQue="La comprobacion se hace antes de cada caso: revisa que no haya fugas y que los sensores funcionen." },

            // ---- Flujometro ----
            new PasoRutina { id="abrirO2", piezaId=P_O2, accion=TipoAccion.Presionar, segundos=3f,
                instruccion="Paso 3: Gira la Perilla de flujo de O2 para abrirla y observa como sube el flotador del flujometro.",
                porQue="Se verifica que hay O2 en la toma y que el flujometro responde: el flotador debe subir suave, sin atorarse ni brincar.",
                textoEspera="Observa el flotador del flujometro..." },
            new PasoRutina { id="cerrarO2", piezaId=P_O2, accion=TipoAccion.Presionar, segundos=1.5f,
                instruccion="Paso 4: Regresa la Perilla de flujo de O2 a cero.",
                porQue="Para buscar fugas el circuito se presuriza solo con el O2 rapido; todo el flujo fresco debe estar en cero. Si queda flujo, la presion sube sola y la prueba no vale.",
                textoEspera="Cerrando el flujo de O2..." },

            // ---- Prueba de fugas en modo BAG (manual) ----
            new PasoRutina { id="selBag", piezaId=P_SEL, accion=TipoAccion.Presionar, segundos=1f,
                instruccion="Paso 5: Mueve la Palanca Bag/Vent a la posicion BAG (bolsa).",
                porQue="En BAG el circuito se cierra con la bolsa y la valvula APL, y el fuelle del ventilador queda aislado. Primero se prueba el circuito manual y despues el mecanico." },
            new PasoRutina { id="taponarY", piezaId=P_TAPON, accion=TipoAccion.Agarrar, segundos=0.8f,
                instruccion="Paso 6: Presiona E sobre el Tapon (cuelga junto a la pieza Y) para ponerlo y tapar la Y.",
                porQue="En el paciente la Y se conecta al tubo endotraqueal. Para la prueba se tapa en su lugar: sin tapon el gas escapa por la Y, la presion nunca sube y la prueba no es valida.",
                textoEspera="Colocando el tapon..." },
            new PasoRutina { id="cerrarAPL", piezaId=P_APL, accion=TipoAccion.Presionar, segundos=1.5f,
                instruccion="Paso 7: Cierra la valvula APL girando su perilla hasta el tope (75 cmH2O).",
                porQue="La APL es la salida de seguridad del circuito. Si esta abierta el gas escapa y no se puede presurizar; para la prueba debe estar cerrada.",
                textoEspera="Cerrando la valvula APL..." },
            new PasoRutina { id="llenarBag", piezaId=P_FLUSH, accion=TipoAccion.Mantener,
                instruccion="Paso 8: Manten presionado el Boton de O2 rapido y mira el manometro. Suelta cuando la aguja marque cerca de 30 cmH2O.",
                porQue="El flush mete O2 al circuito a gran flujo. La presion se lee en el manometro de via aerea, no se adivina. 30 cmH2O exige al circuito sin danarlo: la prueba automatica de la maquina acepta de 25 a 35." },
            new PasoRutina { id="mantenerBag", piezaId="", accion=TipoAccion.Esperar, segundos=10f,
                instruccion="Paso 9: Observa el manometro durante 10 segundos: la aguja debe quedarse en su lugar.",
                porQue="Con el circuito sellado la presion no baja. Si la aguja cae hay una fuga: conexiones flojas, bolsa, absorbedor, mangueras o fuelle.",
                textoEspera="Observa el manometro: la presion se mantiene." },
            new PasoRutina { id="abrirAPL", piezaId=P_APL, accion=TipoAccion.Presionar, segundos=3f,
                instruccion="Paso 10: Abre la valvula APL (gira su perilla a SP) y observa como baja la presion.",
                porQue="Al abrir la APL la presion debe caer rapido a cero. Asi se comprueba que la valvula funciona y que el circuito no queda presurizado.",
                textoEspera="Liberando la presion por la APL..." },

            // ---- Prueba de fugas en modo VENT (mecanico) ----
            new PasoRutina { id="selVent", piezaId=P_SEL, accion=TipoAccion.Presionar, segundos=1f,
                instruccion="Paso 11: Mueve la Palanca Bag/Vent a la posicion VENT (ventilador).",
                porQue="En VENT el fuelle entra al circuito y la APL queda fuera. Ahora se prueba el circuito del ventilador mecanico, con la Y todavia tapada." },
            new PasoRutina { id="llenarVent", piezaId=P_FLUSH, accion=TipoAccion.Mantener,
                instruccion="Paso 12: Manten presionado el Boton de O2 rapido hasta que el fuelle (concertina) suba por completo.",
                porQue="El O2 rapido llena el fuelle. Con la Y tapada el fuelle debe subir hasta su tope y la presion no pasar de 15 cmH2O." },
            new PasoRutina { id="mantenerVent", piezaId="", accion=TipoAccion.Esperar, segundos=10f,
                instruccion="Paso 13: Observa el fuelle durante 10 segundos: no debe bajar.",
                porQue="Si el fuelle baja con el boton suelto, escapa gas: puede haber fuga en el fuelle, en su carcasa o en el circuito.",
                textoEspera="Observa el fuelle: se mantiene arriba." },
            new PasoRutina { id="continuarAuto", piezaId=P_PRINCIPAL, accion=TipoAccion.Presionar, segundos=12f,
                instruccion="Paso 14: Presiona la Perilla principal para iniciar la prueba automatica de fugas y compliance.",
                porQue="Con la Y tapada y el fuelle lleno, la maquina mide sola la fuga y la compliance del circuito (se acepta una fuga de hasta 200 mL/min).",
                textoEspera="La maquina mide fugas y compliance..." },

            // ---- Prueba del ventilador con pulmon de prueba ----
            new PasoRutina { id="quitarTapon", piezaId=P_TAPON, accion=TipoAccion.Agarrar, segundos=0.8f,
                instruccion="Paso 15: Presiona E sobre el Tapon para quitarlo y dejar libre la pieza Y.",
                porQue="La prueba de fugas ya termino. Para probar el ventilador la Y debe quedar libre para conectar un pulmon de prueba.",
                textoEspera="Quitando el tapon..." },
            new PasoRutina { id="conectarPulmon", piezaId=P_BOLSA, accion=TipoAccion.Agarrar, segundos=1.2f,
                instruccion="Paso 16: Presiona E sobre la Bolsa para conectarla a la pieza Y como pulmon de prueba.",
                porQue="La bolsa hace las veces de los pulmones de un paciente: sin ella el ventilador no tiene a quien insuflar y no se puede ver su desempeno. En VENT el brazo de la bolsa queda aislado del circuito, por eso puede retirarse sin crear una fuga.",
                textoEspera="Conectando el pulmon de prueba..." },
            new PasoRutina { id="iniciarVent", piezaId=P_VENT, accion=TipoAccion.Presionar, segundos=1f,
                instruccion="Paso 17: Presiona el Boton de ventilacion para iniciar el ventilador.",
                porQue="Se pone en marcha la ventilacion mecanica con parametros de prueba: 500 mL, 12 respiraciones por minuto y relacion inspiracion:espiracion de 1:2.",
                textoEspera="Iniciando ventilacion..." },
            new PasoRutina { id="observarVent", piezaId="", accion=TipoAccion.Esperar, segundos=25f,
                instruccion="Paso 18: Observa la respiracion de la maquina.",
                porQue="El volumen que baja el fuelle es el que entra al pulmon. Debe haber un movimiento regular, una presion maxima razonable (unos 15 cmH2O) y regreso completo a la linea base al espirar.",
                textoEspera="Observa: el fuelle baja al inspirar y sube al espirar; el pulmon se infla y se desinfla; la aguja oscila." },
            new PasoRutina { id="detenerVent", piezaId=P_VENT, accion=TipoAccion.Presionar, segundos=1.5f,
                instruccion="Paso 19: Presiona el Boton de ventilacion para detener el ventilador.",
                porQue="La prueba termino. Se detiene el ventilador antes de dejar la maquina lista para el caso.",
                textoEspera="Deteniendo el ventilador..." },
            new PasoRutina { id="devolverPulmon", piezaId=P_BOLSA, accion=TipoAccion.Agarrar, segundos=1f,
                instruccion="Paso 20: Presiona E sobre la Bolsa para regresarla a su puerto.",
                porQue="Terminada la prueba, la bolsa vuelve a su puerto: es la que se usa para ventilar a mano al paciente.",
                textoEspera="Regresando la bolsa a su puerto..." },
            new PasoRutina { id="selBagFin", piezaId=P_SEL, accion=TipoAccion.Presionar, segundos=1f,
                instruccion="Paso 21: Regresa la Palanca Bag/Vent a BAG.",
                porQue="La maquina se deja en manual, con la APL abierta, para poder ventilar a mano de inmediato si algo falla durante la induccion." },
        };
    }

    // Si se activa "agarrarPiezas" los pasos de poner piezas piden mantener E y soltar en la zona verde
    void AplicarTextosDeAgarre()
    {
        if (!agarrarPiezas) return;
        for (int i = 0; i < pasos.Count; i++)
        {
            PasoRutina p = pasos[i];
            if (p.accion != TipoAccion.Agarrar) continue;
            string n = Nombre(p.piezaId);
            p.instruccion = p.instruccion.Substring(0, p.instruccion.IndexOf(':') + 1) +
                " Manten presionada la tecla E sobre " + n + ", llevala a la zona verde y sueltala ahi.";
        }
    }

    // ================= Busqueda de piezas =================
    Transform Buscar(string nombre)
    {
        if (raizMaquina == null)
        {
            GameObject g = GameObject.Find("anestesia2");
            if (g != null) raizMaquina = g.transform;
        }
        if (raizMaquina != null)
        {
            Transform[] todos = raizMaquina.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < todos.Length; i++)
                if (todos[i].name == nombre) return todos[i];
        }
        GameObject f = GameObject.Find(nombre);
        return f != null ? f.transform : null;
    }

    void BuscarPiezas()
    {
        tPrincipal = Buscar(P_PRINCIPAL);
        tO2 = Buscar(P_O2);
        tAPL = Buscar(P_APL);
        tFlush = Buscar(P_FLUSH);
        tVent = Buscar(P_VENT);
        tSel = Buscar(P_SEL);

        cv = GetComponent<CircuitoVisual>();
        if (cv == null) cv = gameObject.AddComponent<CircuitoVisual>();
        cv.Iniciar(raizMaquina, Buscar);

        // toma de gases en la pared y mangueras con codigo de colores (si algo falla no afecta la rutina)
        try
        {
            GasesMedicinales gases = GetComponent<GasesMedicinales>();
            if (gases == null) gases = gameObject.AddComponent<GasesMedicinales>();
            gases.Iniciar(raizMaquina, Buscar, cv.Unidad, cv.Frente);
        }
        catch (System.Exception e) { Debug.LogWarning("[Rutina] No se pudo crear la toma de gases: " + e.Message); }

        PrepararPieza(tPrincipal, P_PRINCIPAL, true);
        PrepararPieza(tO2, P_O2, true);
        PrepararPieza(tAPL, P_APL, true);
        PrepararPieza(tFlush, P_FLUSH, true);
        PrepararPieza(tVent, P_VENT, true);
        PrepararPieza(tSel, P_SEL, true);
        PrepararPieza(cv.Tapon, P_TAPON, false);
        PrepararPieza(cv.Bolsa, P_BOLSA, false);
    }

    // Agrega SequencePiece y collider si hacen falta
    void PrepararPieza(Transform t, string id, bool agrandar)
    {
        if (t == null) { Debug.LogWarning("[Rutina] No encontre la pieza " + id); return; }
        SequencePiece sp = t.GetComponent<SequencePiece>();
        if (sp == null) sp = t.gameObject.AddComponent<SequencePiece>();
        sp.pieceId = id;
        if (t.GetComponent<Collider>() == null)
        {
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                MeshCollider mc = t.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
            else t.gameObject.AddComponent<BoxCollider>();
        }
        if (agrandar) AgrandarCollider(t);
    }

    // Las piezas muy chicas (perillas, palanca, botones) se apuntan mejor con una zona de toque un poco mayor
    void AgrandarCollider(Transform t)
    {
        if (cv == null) return;
        Renderer[] rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        float minimo = 1.6f * cv.Unidad;
        float semi = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
        if (semi >= minimo) return;
        SphereCollider sc = t.gameObject.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.center = t.InverseTransformPoint(b.center);
        Vector3 ls = t.lossyScale;
        float esc = Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
        sc.radius = minimo / Mathf.Max(1e-6f, esc);
    }

    // ================= Pantalla =================
    void PonerSprite(Sprite sp)
    {
        if (pantallaImage == null) return;
        pantallaImage.gameObject.SetActive(true);
        pantallaImage.enabled = true;
        Canvas cn = pantallaImage.canvas;
        if (cn != null) cn.enabled = true;
        if (sp == null)
        {
            pantallaImage.sprite = null;
            pantallaImage.color = new Color(0.01f, 0.01f, 0.02f, 1f);
        }
        else
        {
            pantallaImage.sprite = sp;
            pantallaImage.color = Color.white;
        }
    }

    void VApagada()
    {
        if (pantalla != null) pantalla.Apagada(); else PonerSprite(null);
    }

    void VStandby()
    {
        if (pantalla != null) pantalla.Standby(); else PonerSprite(spriteStandby);
    }

    void VLista(int[] etapas, string[] items, bool[] hechos)
    {
        if (pantalla != null) pantalla.Lista(etapas, items, hechos); else PonerSprite(spriteMenu);
    }

    void VPrueba(int[] etapas, string titulo, string etiqueta, float frac, string detalles, bool aprob)
    {
        if (pantalla != null) { pantalla.Prueba(etapas, titulo, etiqueta, frac, detalles, aprob); return; }
        if (aprob) PonerSprite(spriteOkay);
        else if (etiqueta.StartsWith("Preparando")) PonerSprite(spriteMenu);
        else PonerSprite(spriteChequeoProgreso);
    }

    // ================= Efectos =================
    IEnumerator Anim(float dur, System.Action<float> f)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            f(Mathf.Clamp01(t / dur));
            yield return null;
        }
        f(1f);
    }

    // Ciclo respiratorio del ventilador de prueba
    void CicloVent()
    {
        float t = (Time.time - ventT0) % VENT_T;
        float vol;
        if (t < VENT_TI) vol = Mathf.SmoothStep(0f, 1f, t / VENT_TI);   // inspiracion: baja el fuelle, se infla el pulmon
        else vol = Mathf.Exp(-(t - VENT_TI) / 0.7f);                     // espiracion pasiva: vuelve a la linea base
        volVent = vol;
        presion = VENT_PEEP + (VENT_PICO - VENT_PEEP) * vol;
        if (cv != null)
        {
            cv.SetFuelle(BELL_LLENO - BELL_VT_K * vol);          // el fuelle baja un tercio de su recorrido al inspirar
            cv.SetBolsaForma(Mathf.Lerp(BOLSA_ANCHO0, BOLSA_ANCHO1, vol), Mathf.Lerp(BOLSA_LARGO0, BOLSA_LARGO1, vol));   // el pulmon se estira hacia abajo
        }
    }

    // ================= Flujo =================
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) Reiniciar();

        if (esperaRestante > 0f && !terminado)
        {
            esperaRestante -= Time.deltaTime;
            if (esperaRestante <= 0f)
            {
                esperaRestante = 0f;
                ocupado = false;
                FinEspera();
            }
            else ActualizarEspera();
        }

        if (llevando) ActualizarCarga();

        // se solto el boton que se mantenia
        if (sosteniendo && Time.time - ultimoHold > 0.15f)
        {
            sosteniendo = false;
            AlSoltar();
        }

        if (ventilando) CicloVent();

        if (cv != null && cv.Listo) cv.SetAguja(presion);
        if (pantalla != null) { pantalla.Paw(presion); pantalla.Tick(); }

        if (mensajeTimer > 0f)
        {
            mensajeTimer -= Time.deltaTime;
            if (mensajeTimer <= 0f && textoMensaje != null)
            {
                textoMensaje.text = "";
                if (cajaMensaje != null) cajaMensaje.SetActive(false);
            }
        }
    }

    public void Reiniciar()
    {
        StopAllCoroutines();
        idx = 0; ocupado = false; terminado = false; iniciada = false; errores = 0;
        esperaRestante = 0f; hold = 0f; presion = 0f; presionBase = 0f; enVistaLista = false;
        sosteniendo = false; ventilando = false; volVent = 0f; yTapada = false;
        llevando = false; idLlevado = "";
        for (int i = 0; i < chk.Length; i++) chk[i] = false;

        if (cv != null) { cv.Reiniciar(); cv.SetFuelle(BELL_VACIO); }
        VApagada();
        if (textoMensaje != null) textoMensaje.text = "";
        if (cajaMensaje != null) cajaMensaje.SetActive(false);
        SetInstruccion(TextoPaso(pasos[0]));
        Debug.Log("[Rutina] Reiniciada. Presiona R para reiniciar en cualquier momento.");
    }

    void Avanzar()
    {
        idx++;
        hold = 0f;
        sosteniendo = false;
        if (idx >= pasos.Count) { Terminar(); return; }
        SetInstruccion(TextoPaso(pasos[idx]));
        if (pasos[idx].accion == TipoAccion.Esperar) IniciarEspera(pasos[idx].segundos);
    }

    void Terminar()
    {
        terminado = true;
        ventilando = false;
        float t = Time.time - tiempoInicio;
        int min = (int)(t / 60f);
        int seg = (int)(t % 60f);
        VPrueba(new[] { 2, 2, 2, 2 }, "Comprobacion del sistema", "Comprobacion completada", 1f, "Todas las pruebas aprobadas.", true);
        SetInstruccion("Rutina completada!\nTiempo: " + min + ":" + seg.ToString("00") + "   Errores: " + errores + "\n(R para reiniciar)");
        Mensaje("", Color.white);
        Debug.Log("[Rutina] Terminada. Tiempo " + t.ToString("F1") + " s, errores " + errores);
    }

    void IniciarEspera(float seg)
    {
        ocupado = true;
        esperaTotal = Mathf.Max(0.01f, seg);
        esperaRestante = esperaTotal;
        presionBase = presion;
        ActualizarEspera();
    }

    void ActualizarEspera()
    {
        PasoRutina p = pasos[idx];
        float frac = 1f - esperaRestante / esperaTotal;
        switch (p.id)
        {
            case "mantenerBag":
                // Circuito sellado: la aguja casi no se mueve (apenas tiembla)
                presion = presionBase - 0.1f * frac + Mathf.Sin(Time.time * 7f) * 0.06f;
                if (cv != null) cv.SetBolsa(presion / 30f);
                break;
            case "mantenerVent":
                presion = 3f + Mathf.Sin(Time.time * 6f) * 0.05f;
                break;
            case "continuarAuto":
                presion = 5f;
                VPrueba(new[] { 2, 2, 1, 0 }, "Prueba automatica de fugas", "Prueba en curso", frac,
                        Detalles(frac, "Fugas del circuito", "Compliance del circuito", "Sensor de presion"), false);
                break;
            case "iniciarVent":
            case "observarVent":
                VPrueba(new[] { 2, 2, 2, 1 }, "Prueba del ventilador", "Prueba en curso", frac,
                        Detalles(frac, "Volumen corriente", "Presion de vias aereas", "Alarmas del ventilador"), false);
                break;
        }
        string baseTxt = string.IsNullOrEmpty(p.textoEspera) ? p.instruccion : p.textoEspera;
        SetInstruccion(TextoPaso(p, baseTxt + " (" + Mathf.CeilToInt(esperaRestante) + " s)"));
    }

    static string Detalles(float frac, string a, string b, string c)
    {
        string[] n = { a, b, c };
        System.Text.StringBuilder sb = new System.Text.StringBuilder("Detalles:\n");
        for (int i = 0; i < 3; i++)
        {
            string estado = frac >= (i + 1) / 3f ? "<color=#33E64D>Aprob</color>" : (frac >= i / 3f ? "En curso" : "");
            sb.Append(n[i]).Append("<pos=70%>").Append(estado).Append('\n');
        }
        return sb.ToString();
    }

    void FinEspera()
    {
        PasoRutina p = pasos[idx];
        switch (p.id)
        {
            case "encender":
                VStandby();
                break;
            case "mantenerBag":
                chk[2] = true;
                MostrarLista();
                Mensaje("La presion se mantuvo: no hay fugas en modo BAG.", Color.green);
                break;
            case "abrirAPL":
                presion = 0f;
                break;
            case "mantenerVent":
                chk[3] = true;
                MostrarLista();
                Mensaje("El fuelle se mantuvo arriba: no hay fugas en modo VENT.", Color.green);
                break;
            case "continuarAuto":
                presion = 3f;
                VPrueba(new[] { 2, 2, 2, 0 }, "Prueba automatica de fugas", "Prueba completada", 1f,
                        Detalles(1f, "Fugas del circuito", "Compliance del circuito", "Sensor de presion"), true);
                Mensaje("Prueba automatica de fugas: aprobada (fuga menor a 200 mL/min).", Color.green);
                break;
            case "observarVent":
                Mensaje("Ventilacion regular: el ventilador funciona correctamente.", Color.green);
                break;
            case "detenerVent":
                presion = 0f;
                VPrueba(new[] { 2, 2, 2, 2 }, "Prueba del ventilador", "Prueba completada", 1f,
                        Detalles(1f, "Volumen corriente", "Presion de vias aereas", "Alarmas del ventilador"), true);
                break;
        }
        Avanzar();
    }

    void MostrarLista()
    {
        if (enVistaLista)
            VLista(new[] { 2, 1, 0, 0 }, ItemsLista, chk);
    }

    // ================= Interaccion =================
    public void OnPiezaPresionada(string pieza)
    {
        if (terminado || ocupado || llevando) return;
        PasoRutina p = pasos[idx];
        if (p.accion == TipoAccion.Esperar) return;

        if (pieza != p.piezaId)
        {
            errores++;
            if (pieza == P_FLUSH && !yTapada)
                Mensaje("El O2 rapido se escapa por la Y abierta: la presion no sube. Ahora debes usar: " + Nombre(p.piezaId) + ". Errores: " + errores, Color.red);
            else
                Mensaje("Tocaste: " + Nombre(pieza) + ". Ahora debes usar: " + Nombre(p.piezaId) + ". Errores: " + errores, Color.red);
            return;
        }
        if (p.accion == TipoAccion.Mantener) return; // se completa manteniendo E
        if (p.accion == TipoAccion.Agarrar)
        {
            if (agarrarPiezas) { IniciarCarga(pieza); return; }
            // un solo toque: la pieza se pone sola
            IniciarCronometro();
            if (cv != null) cv.Colocar(pieza);
            EjecutarPresion(p);
            if (p.segundos > 0f) IniciarEspera(p.segundos); else Avanzar();
            return;
        }

        IniciarCronometro();
        EjecutarPresion(p);
        if (p.segundos > 0f) IniciarEspera(p.segundos);
        else Avanzar();
    }

    void EjecutarPresion(PasoRutina p)
    {
        switch (p.id)
        {
            case "iniciar":
                enVistaLista = true;
                MostrarLista();
                break;
            case "abrirO2":
                cv.GirarO2(true);
                StartCoroutine(Anim(2f, k => cv.SetFlotador(Mathf.Lerp(0f, 0.6f, k))));
                break;
            case "cerrarO2":
                cv.GirarO2(false);
                StartCoroutine(Anim(1.5f, k => cv.SetFlotador(Mathf.Lerp(0.6f, 0f, k))));
                chk[0] = true;
                MostrarLista();
                break;
            case "selBag":
                cv.GirarPalanca(false);
                break;
            case "taponarY":
                yTapada = true;
                break;
            case "cerrarAPL":
                cv.GirarAPL(true);
                chk[1] = true;
                MostrarLista();
                break;
            case "abrirAPL":
            {
                cv.GirarAPL(false);
                float p0 = presion;
                StartCoroutine(Anim(2.5f, k =>
                {
                    presion = Mathf.Lerp(p0, 0f, k);
                    cv.SetBolsa(presion / 30f);
                }));
                break;
            }
            case "selVent":
                cv.GirarPalanca(true);
                break;
            case "continuarAuto":
                enVistaLista = false;
                break;
            case "quitarTapon":
                yTapada = false;
                break;
            case "conectarPulmon":
                // el pulmon de prueba queda con un poco de aire
                StartCoroutine(Anim(1f, k => cv.SetBolsaForma(Mathf.Lerp(BOLSA_VACIA, BOLSA_ANCHO0, k), Mathf.Lerp(BOLSA_VACIA, BOLSA_LARGO0, k))));
                break;
            case "devolverPulmon":
                StartCoroutine(Anim(0.9f, k => cv.SetBolsaForma(Mathf.Lerp(BOLSA_ANCHO0, BOLSA_VACIA, k), Mathf.Lerp(BOLSA_LARGO0, BOLSA_VACIA, k))));
                break;
            case "iniciarVent":
                ventilando = true;
                ventT0 = Time.time;
                break;
            case "detenerVent":
            {
                ventilando = false;
                float v0 = volVent;
                StartCoroutine(Anim(1.2f, k =>
                {
                    float v = Mathf.Lerp(v0, 0f, k);
                    presion = Mathf.Lerp(VENT_PEEP + (VENT_PICO - VENT_PEEP) * v0, 0f, k);
                    cv.SetFuelle(BELL_LLENO - BELL_VT_K * v);
                    cv.SetBolsaForma(Mathf.Lerp(BOLSA_ANCHO0, BOLSA_ANCHO1, v), Mathf.Lerp(BOLSA_LARGO0, BOLSA_LARGO1, v));
                }));
                break;
            }
            case "selBagFin":
                cv.GirarPalanca(false);
                break;
        }
    }

    // Se llama cada frame mientras se mantiene E sobre la pieza
    public void OnBotonMantenido(string pieza)
    {
        if (terminado || ocupado || llevando) return;
        PasoRutina p = pasos[idx];
        if (p.accion != TipoAccion.Mantener || pieza != p.piezaId) return;

        IniciarCronometro();
        sosteniendo = true;
        ultimoHold = Time.time;

        if (p.id == "llenarBag")
        {
            // Con la Y tapada y la APL cerrada la presion sube unos 10 cmH2O por segundo
            presion = Mathf.Min(40f, presion + 10f * Time.deltaTime);
            cv.SetBolsa(presion / 30f);
            if (presion > 35f) Mensaje("Con 30 cmH2O basta: suelta el boton.", Color.yellow);
        }
        else if (p.id == "llenarVent")
        {
            hold = Mathf.Min(1f, hold + Time.deltaTime / 3f);
            cv.SetFuelle(Mathf.Lerp(BELL_VACIO, BELL_LLENO, hold));
            presion = 2f + 8f * hold;
            if (hold >= 1f)
            {
                sosteniendo = false;
                presion = 4f;
                Mensaje("El fuelle llego a su tope.", Color.green);
                Avanzar();
            }
        }
    }

    // Se llama cuando el alumno suelta el boton que mantenia
    void AlSoltar()
    {
        if (terminado || idx >= pasos.Count) return;
        PasoRutina p = pasos[idx];
        if (p.id == "llenarBag")
        {
            if (presion >= 25f)
            {
                Mensaje(presion > 35f
                    ? "Presion algo alta (" + presion.ToString("F0") + " cmH2O): se acepta, pero lo ideal es cerca de 30."
                    : "Presion correcta: " + presion.ToString("F0") + " cmH2O.", Color.green);
                Avanzar();
            }
            else
                Mensaje("Todavia no llega a 30 cmH2O (va en " + presion.ToString("F0") + "). Manten presionado de nuevo.", Color.yellow);
        }
        else if (p.id == "llenarVent")
        {
            Mensaje("El fuelle aun no llega arriba. Manten presionado de nuevo.", Color.yellow);
        }
    }

    // Compatibilidad con MovablePart (ya no se usa)
    public void OnPartReachedTarget() { }

    public bool EsPiezaActual(string pieza)
    {
        if (terminado || ocupado || pasos == null || idx >= pasos.Count) return false;
        PasoRutina p = pasos[idx];
        if (llevando) return pieza == idLlevado;
        return p.accion != TipoAccion.Esperar && p.piezaId == pieza;
    }

    // ================= Llevar piezas en la mano =================
    void IniciarCarga(string id)
    {
        if (cv == null || !cv.PuedeAgarrar(id)) return;
        if (camara == null) camara = Camera.main;
        if (camara == null) return;
        llevando = true;
        idLlevado = id;
        cv.IniciarAgarre(id);
        manoPos = cv.PosRef(id);
        Transform ct = camara.transform;
        distMano = Mathf.Clamp(Vector3.Dot(manoPos - ct.position, ct.forward), 0.35f, 3f);
        IniciarCronometro();
        Mensaje("Lleva " + Nombre(id) + " a la zona verde y suelta la tecla E.", Color.cyan);
    }

    // La pieza sigue la mirada; cerca de la zona verde se atrae y se orienta sola
    void ActualizarCarga()
    {
        if (camara == null) camara = Camera.main;
        if (camara == null || cv == null) { SoltarCarga(false); return; }
        Transform ct = camara.transform;
        Vector3 dest = cv.DestinoRef(idLlevado);
        float iman = cv.RadioIman(idLlevado);
        float encaje = cv.RadioEncaje(idLlevado);

        // si el destino esta al alcance, la pieza viaja a esa profundidad para que apuntar a la zona verde baste
        float dT = Vector3.Dot(dest - ct.position, ct.forward);
        float dObj = (dT > 0.45f && dT < 2f) ? dT : 0.7f;
        distMano = Mathf.Lerp(distMano, dObj, 1f - Mathf.Exp(-8f * Time.deltaTime));
        Vector3 objetivo = ct.position + ct.forward * distMano;
        manoPos = Vector3.Lerp(manoPos, objetivo, 1f - Mathf.Exp(-14f * Time.deltaTime));

        float d = Vector3.Distance(manoPos, dest);
        float w = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(d / Mathf.Max(1e-5f, iman)));
        cv.MoverAMano(idLlevado, Vector3.Lerp(manoPos, dest, w * 0.8f), w);

        if (!Input.GetKey(KeyCode.E)) SoltarCarga(d <= encaje);
    }

    void SoltarCarga(bool exito)
    {
        string id = idLlevado;
        llevando = false;
        idLlevado = "";
        PasoRutina p = pasos[idx];
        if (cv != null) cv.Soltar(id, exito);
        if (exito)
        {
            Mensaje("Bien colocado.", Color.green);
            EjecutarPresion(p);
            if (p.segundos > 0f) IniciarEspera(p.segundos);
            else Avanzar();
        }
        else
        {
            Mensaje("Llevala hasta la zona verde antes de soltar la tecla E.", Color.yellow);
        }
    }

    // ================= Textos =================
    string Nombre(string id)
    {
        string n;
        return Amigables.TryGetValue(id, out n) ? n : id;
    }

    void IniciarCronometro()
    {
        if (iniciada) return;
        iniciada = true;
        tiempoInicio = Time.time;
    }

    string TextoPaso(PasoRutina p, string extra = null)
    {
        string t = (extra != null) ? extra : p.instruccion;
        if (!string.IsNullOrEmpty(p.porQue))
            t += "\n<size=80%><color=#FFD54F>Por que: " + p.porQue + "</color></size>";
        return t;
    }

    void SetInstruccion(string t) { if (textoInstruccion != null) textoInstruccion.text = t; }

    void Mensaje(string t, Color c)
    {
        if (textoMensaje == null) return;
        textoMensaje.text = t;
        textoMensaje.color = c;
        mensajeTimer = 4f;
        if (cajaMensaje != null) cajaMensaje.SetActive(!string.IsNullOrEmpty(t));
    }

    // ================= HUD =================
    void CrearHUD()
    {
        GameObject cgo = new GameObject("HUD_Rutina");
        Canvas canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        if (textoInstruccion == null)
            textoInstruccion = CrearTexto(cgo.transform, "Instruccion", new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(1700, 280), 42, Color.white, true);
        if (textoMensaje == null)
        {
            textoMensaje = CrearTexto(cgo.transform, "Mensaje", new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(1500, 100), 40, Color.yellow, true);
            cajaMensaje = textoMensaje.transform.parent.gameObject;
            cajaMensaje.SetActive(false);
        }
    }

    TMP_Text CrearTexto(Transform padre, string nombre, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, Color color, bool conFondo)
    {
        GameObject cont = new GameObject(nombre + "_Fondo");
        cont.transform.SetParent(padre, false);
        RectTransform rt = cont.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        if (conFondo)
        {
            Image img = cont.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.82f);
            img.raycastTarget = false;
        }

        GameObject go = new GameObject(nombre);
        go.transform.SetParent(cont.transform, false);
        RectTransform trt = go.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(10, 5);
        trt.offsetMax = new Vector2(-10, -5);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }
}
