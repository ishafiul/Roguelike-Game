﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoguelikeRandomDecision : MonoBehaviour, Decision {

	private Brain brain;

	private void Awake()
	{
		brain = GetComponent<Brain>();
	}

	public float[] Decide (List<float> state, List<Camera> observation, float reward, bool done, float[] memory)
	{
		float[] act;
		if(brain.brainParameters.actionSpaceType == StateType.continuous)
		{
			//Continuous is currently unused (we're on Discrete for Roguelike)
			act = new float[3];
			act[0] = Random.Range(-.5f, .5f);
			act[1] = Random.Range(-.5f, .5f);

			act[2] = (float)Random.Range(-40, 2);
		}
		else
		{
			act = new float[1];

			if(Random.Range(0f, 1f) > .01f)
			{
				//move
				//act[0] = (float)Random.Range(0, 5);
				act[0] = 0; //forced to 0, which means stay still - for better training
			}
			else
			{
				//attack
				act[0] = 5f;
			}
		}

		return act;
	}

	public float[] MakeMemory (List<float> state, List<Camera> observation, float reward, bool done, float[] memory)
	{
		return default(float[]);
	}
}
