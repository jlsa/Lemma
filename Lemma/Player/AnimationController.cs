﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ComponentBind;
using Microsoft.Xna.Framework;
using Lemma.Util;

namespace Lemma.Components
{
	public class AnimationController : Component<Main>, IUpdateableComponent
	{
		private class AnimationInfo
		{
			public int Priority;
			public float DefaultStrength;
		}

		// Input properties
		public Property<bool> IsSupported = new Property<bool>();
		public Property<Player.WallRun> WallRunState = new Property<Player.WallRun>();
		public Property<bool> EnableWalking = new Property<bool>();
		public Property<Vector3> LinearVelocity = new Property<Vector3>();
		public Property<Vector2> Movement = new Property<Vector2>();
		public Property<bool> Crouched = new Property<bool>();
		public Property<Vector2> Mouse = new Property<Vector2>();
		public Property<bool> EnableLean = new Property<bool>();

		// Output
		public Property<float> Lean = new Property<float>();
		private AnimatedModel model;
		private SkinnedModel.Clip sprintAnimation;
		private SkinnedModel.Clip runAnimation;
		private Property<Matrix> relativeHeadBone;
		private Property<Matrix> relativeUpperLeftArm;
		private Property<Matrix> relativeUpperRightArm;
		private Property<Matrix> clavicleLeft;
		private Property<Matrix> clavicleRight;

		private float lastRotation;

		private float breathing;

		public override void Awake()
		{
			base.Awake();
			this.EnabledWhenPaused = false;
			this.lastRotation = this.Mouse.Value.X;
			SoundKiller.Add(this.Entity, AK.EVENTS.STOP_PLAYER_BREATHING_SOFT);
		}

		public void Bind(AnimatedModel m)
		{
			this.model = m;
			this.sprintAnimation = m["Sprint"];
			this.runAnimation = m["Run"];
			this.relativeHeadBone = m.GetRelativeBoneTransform("ORG-head");
			this.clavicleLeft = m.GetBoneTransform("ORG-shoulder_L");
			this.clavicleRight = m.GetBoneTransform("ORG-shoulder_R");
			this.relativeUpperLeftArm = m.GetRelativeBoneTransform("ORG-upper_arm_L");
			this.relativeUpperRightArm = m.GetRelativeBoneTransform("ORG-upper_arm_R");
		}

		// Animations and their priorities
		private Dictionary<string, AnimationInfo> movementAnimations = new Dictionary<string, AnimationInfo>
		{
			{ "Idle", new AnimationInfo { Priority = -1, DefaultStrength = 1.0f, } },
			{ "Run", new AnimationInfo { Priority = 0 } },
			{ "RunBackward", new AnimationInfo { Priority = 0 } },
			{ "RunLeft", new AnimationInfo { Priority = 0 } },
			{ "RunRight", new AnimationInfo { Priority = 0 } },
			{ "Sprint", new AnimationInfo { Priority = 1 } },
		};
		private Dictionary<string, AnimationInfo> crouchMovementAnimations = new Dictionary<string, AnimationInfo>
		{
			{ "CrouchIdle", new AnimationInfo { Priority = 1, DefaultStrength = 1.0f } },
			{ "CrouchWalk", new AnimationInfo { Priority = 2 } },
			{ "CrouchWalkBackward", new AnimationInfo { Priority = 2 } },
			{ "CrouchStrafeLeft", new AnimationInfo { Priority = 2 } },
			{ "CrouchStrafeRight", new AnimationInfo { Priority = 2 } },
		};

		public void Update(float dt)
		{
			Vector2 mouse = this.Mouse;
			if (this.WallRunState != Player.WallRun.None)
			{
				this.model.Stop
				(
					"Jump",
					"JumpLeft",
					"JumpBackward",
					"JumpRight",
					"Fall",
					"Vault",
					"VaultLeft",
					"VaultRight"
				);
				return;
			}

			this.model.Stop
			(
				"WallRunLeft",
				"WallRunRight",
				"WallRunStraight",
				"WallSlideDown",
				"WallSlideReverse"
			);

			if (this.IsSupported)
			{
				this.model.Stop
				(
					"Jump",
					"JumpLeft",
					"JumpBackward",
					"JumpRight",
					"Fall",
					"Vault",
					"VaultLeft",
					"VaultRight"
				);

				Vector2 dir = this.Movement;

				string movementAnimation;

				Vector3 velocity = this.LinearVelocity;
				velocity.Y = 0;
				float speed = velocity.Length();

				if (this.EnableWalking)
				{
					if (this.Crouched)
					{
						if (dir.LengthSquared() == 0.0f)
							movementAnimation = "CrouchIdle";
						else
							movementAnimation = dir.Y < 0.0f ? "CrouchWalkBackward" : (dir.X > 0.0f ? "CrouchStrafeRight" : (dir.X < 0.0f ? "CrouchStrafeLeft" : "CrouchWalk"));
					}
					else
					{
						if (dir.LengthSquared() == 0.0f)
							movementAnimation = "Idle";
						else
							movementAnimation = dir.Y < 0.0f ? "RunBackward" : (dir.X > 0.0f ? "RunRight" : (dir.X < 0.0f ? "RunLeft" : "Run"));
					}
				}
				else
				{
					if (this.Crouched)
						movementAnimation = "CrouchIdle";
					else
						movementAnimation = "Idle";
				}

				foreach (KeyValuePair<string, AnimationInfo> animation in this.Crouched ? crouchMovementAnimations : movementAnimations)
				{
					if (animation.Key != "Idle" && animation.Key != "CrouchIdle")
						this.model[animation.Key].Speed = this.Crouched ? (speed / 2.2f) : (speed / 6.0f);
					this.model[animation.Key].TargetStrength = animation.Key == movementAnimation ? 1.0f : animation.Value.DefaultStrength;
				}

				if (movementAnimation == "Run")
				{
					const float sprintRange = 1.0f;
					const float sprintThreshold = Character.DefaultMaxSpeed - sprintRange;
					this.sprintAnimation.TargetStrength = MathHelper.Clamp((speed - sprintThreshold) / sprintRange, 0.0f, 1.0f);
					this.runAnimation.TargetStrength = Math.Min(MathHelper.Clamp(speed / 4.0f, 0.0f, 1.0f), 1.0f - this.sprintAnimation.TargetStrength);
				}
				else if (movementAnimation != "Idle" && movementAnimation != "CrouchIdle")
					this.model[movementAnimation].TargetStrength = MathHelper.Clamp(this.Crouched ? speed / 2.0f : speed / 4.0f, 0.0f, 1.0f);

				if (!this.model.IsPlaying(movementAnimation))
				{
					foreach (string anim in this.Crouched ? movementAnimations.Keys : crouchMovementAnimations.Keys)
						this.model.Stop(anim);
					foreach (KeyValuePair<string, AnimationInfo> animation in this.Crouched ? crouchMovementAnimations : movementAnimations)
					{
						this.model.StartClip(animation.Key, animation.Value.Priority, true, AnimatedModel.DefaultBlendTime);
						this.model[animation.Key].Strength = animation.Value.DefaultStrength;
					}
				}
			}
			else
			{
				foreach (string anim in movementAnimations.Keys)
					this.model.Stop(anim);
				foreach (string anim in crouchMovementAnimations.Keys)
					this.model.Stop(anim);

				if (!this.model.IsPlaying("Fall"))
					this.model.StartClip("Fall", 0, true, AnimatedModel.DefaultBlendTime);
			}

			// Rotate head and arms to match mouse
			this.relativeHeadBone.Value *= Matrix.CreateRotationX(mouse.Y * 0.4f);
			this.model.UpdateWorldTransforms();

			Matrix r = Matrix.CreateRotationX(mouse.Y * 0.6f * (this.runAnimation.TotalStrength + this.sprintAnimation.TotalStrength));

			Matrix parent = this.clavicleLeft;
			parent.Translation = Vector3.Zero;
			this.relativeUpperLeftArm.Value *= parent * r * Matrix.Invert(parent);

			parent = this.clavicleRight;
			parent.Translation = Vector3.Zero;
			this.relativeUpperRightArm.Value *= parent * r * Matrix.Invert(parent);

			this.model.UpdateWorldTransforms();

			float l = 0.0f;
			if (this.EnableLean)
				l = this.LinearVelocity.Value.Length() * (this.lastRotation.ClosestAngle(mouse.X) - mouse.X);
			this.lastRotation = mouse.X;
			this.Lean.Value += (l - this.Lean) * 20.0f * dt;

			const float timeScale = 5.0f;
			const float softBreathingThresholdPercentage = 0.5f;
			float newBreathing;
			if (!this.Crouched && this.Movement.Value.LengthSquared() > 0.0f && this.LinearVelocity.Value.Length() > 2.0f)
			{
				newBreathing = Math.Min(this.breathing + (dt / timeScale), 1.0f);
				if (this.breathing < softBreathingThresholdPercentage && newBreathing > softBreathingThresholdPercentage)
				{
					AkSoundEngine.PostEvent(AK.EVENTS.PLAY_PLAYER_BREATHING_SOFT, this.Entity);
					newBreathing = 1.0f;
				}
			}
			else
			{
				newBreathing = Math.Max(0, this.breathing - dt / timeScale);
				if (this.breathing > softBreathingThresholdPercentage && newBreathing < softBreathingThresholdPercentage)
					AkSoundEngine.PostEvent(AK.EVENTS.STOP_PLAYER_BREATHING_SOFT, this.Entity);
			}
			this.breathing = newBreathing;
		}
	}
}