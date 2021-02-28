﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoguelikeAgent : Agent
{
	// all variables affecting the behaviour of the agents
	[Header("Roguelike specific")]
	public int speed = 100;
	public int startingHealth = 100;
	public float attackCooldown = 1f;
	public int attackDamage = 5;
	public float searchRadius = 6f;
	public int Health {
		get { return health; }
		set { health = value; healthBar.SetHealth(health, startingHealth); }
	}
	public RoguelikeAgent preassignedTarget;

    protected Rigidbody2D rb;
	protected Animator animator;
	protected Vector2 movementInput; // cached input coming from the Brain
	protected SpriteRenderer graphicsSpriteRenderer;

    [Header("Debug stuff")]
	[SerializeField]
	private RoguelikeAgent targetAgent;

	private bool hasToSearchForTarget = false;
    private int health;
	private float damageCooldown = 1f; // invincibility cooldown after a hit
	private float searchTargetInterval = 2f;
	private float lastSearchTime = -10f;
	private float lastHitTime; // used to verify cooldowns
	private int doAttackHash, isWalkingHash;
	private Color originalColour;
	private bool canAttack = true; // set to false when attacking, restored to true after the attackCooldown
    private bool hasBeenHit = false;
	private Vector2 startPosition;
    private Vector2 movementFactor;
	private bool isHealing;
	private Coroutine healCoroutine;
	private float distanceFromTargetSqr;
	private float thresholdDistanceFromTargetSqr;
	private HealthBar healthBar;
	private RoguelikeAcademy academy;
	private bool isInDanger;

	
    public override void InitializeAgent()
	{
		rb = GetComponent<Rigidbody2D>();
		animator = GetComponent<Animator>();
		graphicsSpriteRenderer = transform.Find("Graphics").GetComponent<SpriteRenderer>();
		healthBar = transform.GetComponentInChildren<HealthBar>();
		doAttackHash = Animator.StringToHash("DoAttack");
		isWalkingHash = Animator.StringToHash("IsWalking");
		startPosition = transform.position;
		originalColour = graphicsSpriteRenderer.color;
		academy = FindObjectOfType<RoguelikeAcademy>();
		if(preassignedTarget != null)
		{
			targetAgent = preassignedTarget;
		}
		else
		{
			hasToSearchForTarget = true; // targetAgent will be looked for in the Update
		}
		
		AgentReset(); // will reset some key variables
	}

	public override List<float> CollectState()
	{
		List<float> state = new List<float>();
		// Agent data
		state.Add(Health * .1f);
		state.Add((canAttack) ? 1f : 0f); // can this Agent attack? (due to attack cooldown)
		state.Add((isInDanger) ? 1f : 0f); // is it better to attack or to run?

		// Enemy data
		if(targetAgent != null)
		{
			state.Add(1f); // does this Agent have an enemy?
			state.Add((targetAgent.rb.position.x - rb.position.x) * .1f); // direction to the enemy on the X
			state.Add((targetAgent.rb.position.y - rb.position.y) * .1f); // direction to the enemy on the Y 
		}
		else
		{
			// enemy data is set to zero
			state.Add(0f);
			state.Add(0f);
			state.Add(0f);
		}
		return state;
	}

	public override void AgentStep(float[] act)
	{
		//reset inputs
		bool attack = false;
		movementInput = Vector2.zero;

		if(brain.brainParameters.actionSpaceType == StateType.discrete)
		{
			if(act[0] == 0f)
			{
				//do nothing
			}
			if(act[0] == 1f)
			{
				movementInput.x = -1f;
			}
			if(act[0] == 2f)
			{
				movementInput.x = 1f;
			}
			if(act[0] == 3f)
			{
				movementInput.y = 1f;
			}
			if(act[0] == 4f)
			{
				movementInput.y = -1f;
			}
			if(act[0] == 5f)
			{
				attack = true;
			}
		}
		else
		{
			movementInput.x = Mathf.Clamp(act[0], -1f, 1f);
			movementInput.y = Mathf.Clamp(act[1], -1f, 1f);
			attack = act[2] > 0f;
		}

		//MOVEMENT
		movementFactor = new Vector2(movementInput.x, movementInput.y) * Time.fixedDeltaTime * speed;
		rb.position += (Vector2)movementFactor;
		//Vector2 parentPos = (parentTransform != null) ? (Vector2)parentTransform.position : Vector2.zero; //calculating parent offset for obtaining local RB coordinates below
		//rbLocalPosition = (Vector2)rb.position - parentPos + movementFactor;

		isInDanger = Health < startingHealth * .7f;

		//DISTANCE CHECK
		if (targetAgent != null)
		{
			distanceFromTargetSqr = GetDistanceFromTargetSqr();
			//movementTowardsTarget = Vector2.Dot(movementInput.normalized, (targetAgent.rb.position-rb.position).normalized); //-1f if moving away, 1f if moving closer
			
			if (!isInDanger)
			{
				//pursue
				if(distanceFromTargetSqr < thresholdDistanceFromTargetSqr)
				{
					reward += .04f;	//.2f
					thresholdDistanceFromTargetSqr = distanceFromTargetSqr;
				}
				else
				{
					reward -= .02f;	//-.2f
				}
			}
			else
			{
				//retreat
				if(distanceFromTargetSqr > thresholdDistanceFromTargetSqr)
				{
					reward += .04f;	//.2f
					thresholdDistanceFromTargetSqr = distanceFromTargetSqr;
				}
				else
				{
					reward -= .02f;	//-.2f
				}
			}
		}
		
		//ATTACK
		if(attack)
		{
			/*if(canAttack)
			{
				StartCoroutine(DoAttack());
			}
			else
			{
				reward = -.1f; //penalty for trying to attack when it can't
			}*/

			if (distanceFromTargetSqr <= 3.5f)
			{
				StartCoroutine(DoAttack());
			}
			else
			{
				reward = -.1f;
			}
		}
		else
		{
			//we don't heal during training, to avoid confusion
			if (brain.brainType != BrainType.External)
			{
				//if not attacking, can start healing
				if(!isHealing
					&& Health < startingHealth)
				{
					healCoroutine = StartCoroutine(Heal());
				}
			}
		}
	}

	private IEnumerator Heal()
	{
		isHealing = true;
		thresholdDistanceFromTargetSqr = 0f;
		yield return new WaitForSeconds(2f);
		
		while(isHealing
			&& Health < startingHealth)
		{	
			//heal
			Health++;
			yield return new WaitForSeconds(2f);
		}

		thresholdDistanceFromTargetSqr = Mathf.Infinity;
		isHealing = false;
	}

    private IEnumerator DoAttack()
    {
		canAttack = false;
        animator.SetTrigger(doAttackHash);

		yield return new WaitForSeconds(attackCooldown);

		canAttack = true;
    }

    public void DealDamage(RoguelikeAgent target)
	{
		bool isTargetDead = false;

		if(!target.hasBeenHit)
		{
			reward += 1f;
			isTargetDead = target.ReceiveDamage(attackDamage);
			if(isTargetDead
				&& !academy.isInference)
			{
				done = true;
			}
		}

	}

	//Returns if the Agent is dead or not, to reward the attacker
    public bool ReceiveDamage(int attackDamage)
    {
        Health -= attackDamage;
		//UIManager.Instance.ShowDamageText(attackDamage, this.transform.position);

		reward = -.5f;
		if(Health <= 0)
		{
			Die();
			return true;
		}
		else
		{
			StartCoroutine(HitFlicker());
			return false;
		}
    }

	private void Die()
	{
		//During training
		done = true;

		if(brain.brainType == BrainType.Internal)
		{
			//During actual gameplay
			Destroy(gameObject);
		}

	}

    private IEnumerator HitFlicker()
    {
		lastHitTime = Time.time;
		hasBeenHit = true;

        while(Time.time < lastHitTime + damageCooldown)
		{
			yield return new WaitForSeconds(.1f);
			graphicsSpriteRenderer.color = Color.red;

			yield return new WaitForSeconds(.1f);
			graphicsSpriteRenderer.color = originalColour;
		}

		hasBeenHit = false;
    }

	public override void AgentReset()
	{
		Health = startingHealth;
		if(brain.brainType == BrainType.External
			|| academy.isInference)
		{
			//fixed position, only for the trainee - or during gameplay
			transform.position = startPosition;
		}
		else
		{
			//randomised position for players, heuristic (the opponents during training)
			float offset = academy.startDistance;
			transform.localPosition = UnityEngine.Random.insideUnitCircle.normalized * offset;
		}
		if(targetAgent != null)
		{
			distanceFromTargetSqr = GetDistanceFromTargetSqr();
		}
		thresholdDistanceFromTargetSqr = Mathf.Infinity;
	}

	public override void AgentOnDone()
	{
		
	}

	private void Update()
	{
		animator.SetBool(isWalkingHash, movementFactor != Vector2.zero);

		if(hasToSearchForTarget)
		{
			//search for a potential target
			float currentTime = Time.time;
			if(currentTime > lastSearchTime + searchTargetInterval)
			{
				if(targetAgent == null)
				{
					//search for a new target (might be null anyway, because of distance)
					targetAgent = SearchForTarget();
				}
				else
				{
					//check if it's too far
					distanceFromTargetSqr = GetDistanceFromTargetSqr();
					if(distanceFromTargetSqr > searchRadius * searchRadius)
					{
						//target lost
						targetAgent = null;
					}
				}
				lastSearchTime = currentTime;
			}
		}
	}

	protected virtual RoguelikeAgent SearchForTarget()
	{
		//this is implemented in inheriting classes
		return null;
	}

	private float GetDistanceFromTargetSqr()
	{
		return (targetAgent.transform.position - transform.position).sqrMagnitude;
	}
}
