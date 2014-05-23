﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using ComponentBind;
using Microsoft.Xna.Framework;
using Lemma.Util;

namespace Lemma.Components
{
	public class RotationController : Component<Main>, IUpdateableComponent
	{
		// Input properties
		[XmlIgnore]
		public Property<WallRun.State> WallRunState = new Property<WallRun.State>();
		[XmlIgnore]
		public Property<Vault.State> VaultState = new Property<Vault.State>();
		[XmlIgnore]
		public Property<bool> Rolling = new Property<bool>();
		[XmlIgnore]
		public Property<bool> Kicking = new Property<bool>();

		// Input/output properties
		public Property<float> Rotation = new Property<float>();
		[XmlIgnore]
		public Property<Vector2> Mouse = new Property<Vector2>();

		// Output properties
		[XmlIgnore]
		public Property<bool> Locked = new Property<bool>();

		const float rotationLockBlendTime = 0.3f;
		float lockedRotationValue = 0.0f;
		float rotationLockBlending = rotationLockBlendTime;

		public override void Awake()
		{
			this.Mouse.Value = new Vector2(this.Rotation, 0.0f);
			this.Rotation.Set = delegate(float value)
			{
				if (this.Locked)
					this.Mouse.Value += new Vector2(value - this.Rotation.InternalValue, 0);
				this.Rotation.InternalValue = value;
			};
			this.Add(new NotifyBinding(this.Unlock, this.Locked, this.WallRunState, this.Kicking, this.Rolling, this.VaultState));
		}

		public void Lock()
		{
			if (!this.Locked)
			{
				this.lockedRotationValue = this.Rotation.Value.ClosestAngle(this.Mouse.Value.X);
				this.rotationLockBlending = 0.0f;
				this.Locked.Value = true;
			}
		}

		public void Unlock()
		{
			if (this.Locked && this.WallRunState.Value == WallRun.State.None && !this.Kicking && !this.Rolling && this.VaultState.Value == Vault.State.None)
			{
				this.rotationLockBlending = 0.0f;
				this.Locked.Value = false;
			}
		}

		public void Update(float dt)
		{
			if (this.rotationLockBlending < rotationLockBlendTime)
				this.rotationLockBlending += dt;

			if (!this.Locked)
			{
				if (this.rotationLockBlending < rotationLockBlendTime)
				{
					this.lockedRotationValue = this.lockedRotationValue.ClosestAngle(this.Mouse.Value.X);
					this.Rotation.Value = this.lockedRotationValue + (this.Mouse.Value.X - lockedRotationValue) * (rotationLockBlending / rotationLockBlendTime);
				}
				else
					this.Rotation.Value = this.Mouse.Value.X;
			}
		}
	}
}
