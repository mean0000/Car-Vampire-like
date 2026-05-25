using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DistantLands.Cozy.Data;
using UnityEngine.AI;

public class AIExampleScript : MonoBehaviour
{

    public Transform homeTarget;
    public Transform workTarget;
    public bool isWorking = true;
    public CozyHabitProfile workHabit;
    public float wanderDistance = 5f;
    public float waitTime = 2f;

    private NavMeshAgent agent;
    private Vector3 targetPosition;
    private bool isMoving = false;
    public float waitTimer = 0f;

    void OnEnable()
    {
        workHabit.onStart += StartWorking;
        workHabit.onEnd += GoHome;
    }
    void OnDisable()
    {
        workHabit.onStart -= StartWorking;
        workHabit.onEnd -= GoHome;
    }

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;

        targetPosition = GetRandomPosition();
    }

    private void Update()
    {
        if (isWorking)
        {
            if (isMoving)
            {
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    isMoving = false;
                    waitTimer = waitTime;
                }
            }
            else
            {
                waitTimer -= Time.deltaTime;

                if (waitTimer <= 0f)
                {
                    targetPosition = GetRandomPosition();
                    MoveToTargetPosition();
                }
            }
        }
        else
        {
            agent.SetDestination(homeTarget.position);
        }
    }

    private void MoveToTargetPosition()
    {
        agent.SetDestination(targetPosition);
        isMoving = true;
    }

    private Vector3 GetRandomPosition()
    {
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        Vector3 targetPos = workTarget.position + new Vector3(randomDirection.x, 0f, randomDirection.y) * wanderDistance;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, wanderDistance, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return workTarget.position;
    }

    public void StartWorking()
    {
        isWorking = true;
    }

    public void GoHome()
    {
        isWorking = false;
    }

}