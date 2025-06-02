using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DragAndDrop : MonoBehaviour
{
    // --------------------------------------------------------------
    // ESTADOS de arrastre
    // --------------------------------------------------------------
    private bool isEnlargeDragging = false;  // true si se está arrastrando con LMB (para agrandar)
    private bool isShrinkDragging  = false;  // true si se está arrastrando con RMB (para encoger)

    // Referencia al objeto clicado (la esfera u otro)
    private GameObject target = null;

    // Punto de impacto en coordenadas LOCALES del objeto
    private Vector3 localHitPoint;

    // ESCENA PARA AMBOS BOTONES: guardamos la escala original y posición del centro
    private Vector3 initialScale;
    private Vector3 initialCenter;

    // Distancia original desde la cámara al CENTRO del objeto
    private float initialDistanceCenter;

    // Posición vertical del ratón en píxeles al iniciar el clic
    private float mouseY_0;

    // --------------------------------------------------------------
    // PARÁMETROS AJUSTABLES (desde el Inspector)
    // --------------------------------------------------------------
    [Header("Sensibilidades de escalado (píxeles → factor)")]
    [SerializeField] private float shrinkSensitivity  = 0.005f;   // Sensibilidad para encoger

    [Header("Límites de escala")]
    [SerializeField] private float minScaleFactor     = 0.1f;     // no permitimos factor < 0.1 (encoger)
    private const float enlargeMinFactor             = 1f;       // en agrandar no bajamos de 1 (escala inicial)

    // --------------------------------------------------------------
    // NUEVOS CAMPOS PARA SONIDO
    // --------------------------------------------------------------
    [Header("Audio Clips")]
    [SerializeField] private AudioClip enlargeClip;              // sonido para agrandar
    [SerializeField] private AudioClip shrinkClip;               // sonido para encoger

    // AudioSource actual que reproducimos en el target (se crea al primer arrastre)
    private AudioSource currentAudioSource = null;

    // --------------------------------------------------------------
    // UMBRAL y Epsilon para evitar “lags”
    // --------------------------------------------------------------
    private const float deltaEpsilon = 0.1f;  // umbral de píxeles para considerar que el ratón NO se ha movido

    void Update()
    {
        // ----------------------------------------
        // 1) MOUSE BUTTON DOWN: LMB o RMB
        // ----------------------------------------

        // ----- LMB (clic izquierdo) inicia “agrandar”
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hitInfo;
            GameObject clicked = GetClickedObject(out hitInfo);
            if (clicked != null)
            {
                target = clicked;
                isEnlargeDragging = true;

                // Guardamos estado inicial común
                mouseY_0 = Input.mousePosition.y;
                initialScale   = target.transform.localScale;
                initialCenter  = target.transform.position;
                initialDistanceCenter = Vector3.Distance(
                    Camera.main.transform.position,
                    target.transform.position
                );
                localHitPoint = target.transform.InverseTransformPoint(hitInfo.point);

                // Desactivamos física mientras arrastramos
                Rigidbody rb = target.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                // ----------------------------
                // SONIDO: Crear/obtener AudioSource y reproducir enlargeClip en bucle
                // ----------------------------
                currentAudioSource = target.GetComponent<AudioSource>();
                if (currentAudioSource == null)
                {
                    currentAudioSource = target.AddComponent<AudioSource>();
                    currentAudioSource.spatialBlend = 0f; // 2D o 3D según prefieras
                    currentAudioSource.playOnAwake = false;
                }
                currentAudioSource.clip = enlargeClip;
                currentAudioSource.loop = true;
                currentAudioSource.Play();
            }
        }

        // ----- RMB (clic derecho) inicia “encoger”
        if (Input.GetMouseButtonDown(1))
        {
            RaycastHit hitInfo;
            GameObject clicked = GetClickedObject(out hitInfo);
            if (clicked != null)
            {
                target = clicked;
                isShrinkDragging = true;

                // Guardamos estado inicial común
                mouseY_0 = Input.mousePosition.y;
                initialScale   = target.transform.localScale;
                initialCenter  = target.transform.position;
                initialDistanceCenter = Vector3.Distance(
                    Camera.main.transform.position,
                    target.transform.position
                );
                localHitPoint = target.transform.InverseTransformPoint(hitInfo.point);

                // Desactivamos física mientras arrastramos
                Rigidbody rb = target.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                // ----------------------------
                // SONIDO: Crear/obtener AudioSource y reproducir shrinkClip en bucle
                // ----------------------------
                currentAudioSource = target.GetComponent<AudioSource>();
                if (currentAudioSource == null)
                {
                    currentAudioSource = target.AddComponent<AudioSource>();
                    currentAudioSource.spatialBlend = 0f; // 2D o 3D según prefieras
                    currentAudioSource.playOnAwake = false;
                }
                currentAudioSource.clip = shrinkClip;
                currentAudioSource.loop = true;
                currentAudioSource.Play();
            }
        }

        // ----------------------------------------
        // 2) MOUSE BUTTON UP: suelta alguno
        // ----------------------------------------

        // Si sueltas LMB, detenemos “agrandar”
        if (Input.GetMouseButtonUp(0) && isEnlargeDragging)
        {
            isEnlargeDragging = false;

            // Restauramos física
            if (target != null)
            {
                Rigidbody rb = target.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = false;
            }

            // STOP AUDIO
            if (currentAudioSource != null)
            {
                currentAudioSource.Stop();
                currentAudioSource = null;
            }

            target = null;
        }

        // Si sueltas RMB, detenemos “encoger”
        if (Input.GetMouseButtonUp(1) && isShrinkDragging)
        {
            isShrinkDragging = false;

            // Restauramos física
            if (target != null)
            {
                Rigidbody rb = target.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = false;
            }

            // STOP AUDIO
            if (currentAudioSource != null)
            {
                currentAudioSource.Stop();
                currentAudioSource = null;
            }

            target = null;
        }

        // ----------------------------------------
        // 3) MIENTRAS ARRASTRAMOS (LMB o RMB):
        //    recalculamos escala + posición
        // ----------------------------------------
        if (isEnlargeDragging && target != null)
        {
            ProcesarArrastre(agrandar: true);
        }
        if (isShrinkDragging && target != null)
        {
            ProcesarArrastre(agrandar: false);
        }
    }

    // --------------------------------------------------------------
    // Función que hace el cálculo tanto para agrandar (agrandar=true)
    // como para encoger (agrandar=false).
    // --------------------------------------------------------------
    private void ProcesarArrastre(bool agrandar)
    {
        // 1) Diferencia vertical actual del ratón
        float mouseY_actual = Input.mousePosition.y;
        float deltaY = mouseY_actual - mouseY_0;

        // Si no hay desplazamiento significativo, restauramos originales
        if (Mathf.Abs(deltaY) < deltaEpsilon)
        {
            target.transform.localScale = initialScale;
            target.transform.position   = initialCenter;
            return;
        }

        float f;
        if (agrandar)
        {
            // --- MODO AGRANDAR usando la lógica recíproca de “reducir” ---
            float f_equivalente_reduccion_raw   = 1f - (deltaY * shrinkSensitivity);
            float f_equivalente_reduccion_clamp = Mathf.Max(f_equivalente_reduccion_raw, minScaleFactor);

            if (Mathf.Approximately(f_equivalente_reduccion_clamp, 0f))
            {
                f = enlargeMinFactor; 
            }
            else
            {
                f = 1f / f_equivalente_reduccion_clamp;
            }

            // Nunca permitimos que f baje de enlargeMinFactor (1)
            f = Mathf.Max(f, enlargeMinFactor);
        }
        else
        {
            // MODO ENCOGER (igual que antes)
            f = 1f - deltaY * shrinkSensitivity;
            f = Mathf.Max(f, minScaleFactor);
        }

        // 2) Nueva escala
        Vector3 newScale = initialScale * f;
        target.transform.localScale = newScale;

        // 3) Nueva distancia al CENTRO de la cámara
        float d_center_new = initialDistanceCenter * f;

        // 4) Trazamos un rayo desde la cámara al pixel actual
        Vector3 mousePosScreen = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0f);
        Ray ray = Camera.main.ScreenPointToRay(mousePosScreen);

        // 5) Hallamos el punto a d_center_new unidades de la cámara
        Vector3 cameraPos         = Camera.main.transform.position;
        Vector3 puntoCentroEnRayo = cameraPos + ray.direction * d_center_new;

        // 6) Desplazamiento local escalado = (localHitPoint * f) rotado por la rotación actual
        Quaternion rot = target.transform.rotation;
        Vector3 desplazLocalEscalado = rot * (localHitPoint * f);

        // 7) Posición final del CENTRO
        Vector3 newWorldPos = puntoCentroEnRayo - desplazLocalEscalado;
        target.transform.position = newWorldPos;
    }

    // --------------------------------------------------------------
    // Raycast helper: retorna el GameObject clicado o null
    // --------------------------------------------------------------
    GameObject GetClickedObject(out RaycastHit hit)
    {
        GameObject clickedObj = null;
        hit = new RaycastHit();
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out hit, 100f))
        {
            clickedObj = hit.collider.gameObject;
        }
        return clickedObj;
    }
}
