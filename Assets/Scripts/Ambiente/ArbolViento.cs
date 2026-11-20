using UnityEngine;

// Ponlo en cada árbol que tenga Animator
[RequireComponent(typeof(Animator))]
public class ArbolViento : MonoBehaviour
{
    [Header("Velocidad normal")]
    [Tooltip("1 = velocidad original de la animación. 0.5 = la mitad de rápido.")]
    public float velocidad = 0.1f;
    [Tooltip("Cada árbol va un poco más rápido o más lento que los demás.")]
    [Range(0f, 0.5f)] public float variacion = 0.15f;

    [Header("Ráfagas de viento")]
    public bool rafagas = true;
    [Tooltip("Cuánto se acelera el árbol cuando le llega la ráfaga.")]
    public float fuerzaRafaga = 1.5f;
    [Tooltip("Segundos entre una ráfaga y la siguiente.")]
    public float cadaCuanto = 6f;
    [Tooltip("Lo rápido que cruza la ráfaga el bosque (unidades por segundo).")]
    public float velocidadRafaga = 8f;
    [Tooltip("Cuánto le dura la ráfaga a cada árbol (segundos).")]
    public float duracionRafaga = 1.5f;

    private Animator animator;
    private float miVelocidad;

    void Start()
    {
        animator = GetComponent<Animator>();

        // Cada árbol con su propia velocidad, un poco distinta
        miVelocidad = velocidad * Random.Range(1f - variacion, 1f + variacion);

        // Empieza en un punto aleatorio de la animación para que no vayan todos a la vez
        AnimatorStateInfo estado = animator.GetCurrentAnimatorStateInfo(0);
        animator.Play(estado.fullPathHash, 0, Random.value);
    }

    void Update()
    {
        float rafaga = 0f;

        if (rafagas)
        {
            // La ráfaga llega antes a los árboles de la izquierda y después a los de la derecha
            float tiempo = Time.time - transform.position.x / velocidadRafaga;
            float fase = Mathf.Repeat(tiempo, cadaCuanto);

            // Durante la ráfaga, la fuerza sube y baja suavemente (0 -> 1 -> 0)
            if (fase < duracionRafaga)
                rafaga = Mathf.Sin(fase / duracionRafaga * Mathf.PI);
        }

        animator.speed = miVelocidad * (1f + rafaga * fuerzaRafaga);
    }
}