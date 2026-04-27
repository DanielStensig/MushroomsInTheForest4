using UnityEngine;
using System.Collections.Generic;

public class BoidManager : MonoBehaviour
{
    [Header("AI Chase Player")]
    public float playerDetectDistance = 8f;
    public float chaseDuration = 4f;
    public float chaseSpeedMultiplier = 2f;

    [Header("Flock Setup")]
    public GameObject boidPrefab;
    public int boidCount = 30;
    public Vector3 spawnArea = new Vector3(20, 10, 20);

    [Header("Movement")]
    public float minSpeed = 3f;
    public float maxSpeed = 6f;
    public float turnSpeed = 3f;

    [Header("Flocking")]
    public float neighborRadius = 5f;
    public float separationRadius = 2f;

    public float cohesionWeight = 1f;
    public float alignmentWeight = 1f;
    public float separationWeight = 2f;

    [Header("Bounds")]
    public float boundsRadius = 25f;
    public float boundsWeight = 4f;

    private readonly List<Boid> boids = new();

    void Start()
    {
        for (int i = 0; i < boidCount; i++)
        {
            Vector3 pos = transform.position + new Vector3(
                Random.Range(-spawnArea.x, spawnArea.x),
                Random.Range(-spawnArea.y, spawnArea.y),
                Random.Range(-spawnArea.z, spawnArea.z)
            );

            GameObject obj = Instantiate(boidPrefab, pos, Random.rotation);
            Boid boid = obj.AddComponent<Boid>();
            boid.manager = this;
            boid.speed = Random.Range(minSpeed, maxSpeed);

            boids.Add(boid);
        }
    }

    public List<Boid> GetBoids()
    {
        return boids;
    }
}

public class Boid : MonoBehaviour
{
    public BoidManager manager;
    public float speed;

    private Transform player;
    private bool chasingPlayer = false;
    private float chaseTimer = 0f;
    private float normalSpeed;

    void Start()
    {
        normalSpeed = speed;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    void Update()
    {
        if (player != null && !chasingPlayer)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= manager.playerDetectDistance)
            {
                chasingPlayer = true;
                chaseTimer = manager.chaseDuration;
                speed = normalSpeed * manager.chaseSpeedMultiplier;
            }
        }

        if (chasingPlayer)
        {
            ChasePlayer();
            return;
        }

        NormalFlocking();
    }

    void ChasePlayer()
    {
        chaseTimer -= Time.deltaTime;

        Vector3 direction = (player.position - transform.position).normalized;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                manager.turnSpeed * Time.deltaTime
            );
        }

        transform.position += transform.forward * speed * Time.deltaTime;

        if (chaseTimer <= 0f)
        {
            chasingPlayer = false;
            speed = normalSpeed;
        }
    }

    void NormalFlocking()
    {
        Vector3 cohesion = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 separation = Vector3.zero;

        int nearbyCount = 0;

        foreach (Boid other in manager.GetBoids())
        {
            if (other == this) continue;

            float distance = Vector3.Distance(transform.position, other.transform.position);

            if (distance < manager.neighborRadius)
            {
                cohesion += other.transform.position;
                alignment += other.transform.forward;
                nearbyCount++;

                if (distance < manager.separationRadius)
                {
                    separation += transform.position - other.transform.position;
                }
            }
        }

        Vector3 direction = transform.forward;

        if (nearbyCount > 0)
        {
            cohesion = ((cohesion / nearbyCount) - transform.position).normalized;
            alignment = (alignment / nearbyCount).normalized;
            separation = separation.normalized;

            direction += cohesion * manager.cohesionWeight;
            direction += alignment * manager.alignmentWeight;
            direction += separation * manager.separationWeight;
        }

        Vector3 centerOffset = manager.transform.position - transform.position;

        if (centerOffset.magnitude > manager.boundsRadius)
        {
            direction += centerOffset.normalized * manager.boundsWeight;
        }

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                manager.turnSpeed * Time.deltaTime
            );
        }

        transform.position += transform.forward * speed * Time.deltaTime;
    }
}