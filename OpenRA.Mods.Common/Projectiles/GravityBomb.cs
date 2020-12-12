#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Projectiles
{
	public class GravityBombInfo : IProjectileInfo
	{
		public readonly string Image = null;

		[SequenceReference(nameof(Image), allowNullImage: true)]
		[Desc("Loop a randomly chosen sequence of Image from this list while falling.")]
		public readonly string[] Sequences = { "idle" };

		[SequenceReference(nameof(Image), allowNullImage: true)]
		[Desc("Sequence to play when launched. Skipped if null or empty.")]
		public readonly string OpenSequence = null;

		[PaletteReference]
		[Desc("The palette used to draw this projectile.")]
		public readonly string Palette = "effect";

		[Desc("Palette is a player palette BaseName")]
		public readonly bool IsPlayerPalette = false;

		public readonly bool Shadow = false;

		[PaletteReference]
		public readonly string ShadowPalette = "shadow";

		[Desc("Projectile movement vector per tick (forward, right, up), use negative values for opposite directions.")]
		public readonly WVec Velocity = WVec.Zero;

		[Desc("Value added to Velocity every tick.")]
		public readonly WVec Acceleration = new WVec(0, 0, -15);

		[Desc("Type defined for point-defense logic.")]
		public readonly string PointDefenseType = null;

		public IProjectile Create(ProjectileArgs args) { return new GravityBomb(this, args); }
	}

	public class GravityBomb : IProjectile, ISync
	{
		readonly GravityBombInfo info;
		readonly Animation anim;
		readonly ProjectileArgs args;
		readonly WVec acceleration;
		WVec velocity;

		[Sync]
		WPos pos, lastPos;

		public GravityBomb(GravityBombInfo info, ProjectileArgs args)
		{
			this.info = info;
			this.args = args;
			pos = args.Source;
			var convertedVelocity = new WVec(info.Velocity.Y, -info.Velocity.X, info.Velocity.Z);
			velocity = convertedVelocity.Rotate(WRot.FromYaw(args.Facing));
			var convertedAcceleration = new WVec(info.Acceleration.Y, -info.Acceleration.X, info.Acceleration.Z);
			acceleration = convertedAcceleration.Rotate(WRot.FromYaw(args.Facing));

			if (!string.IsNullOrEmpty(info.Image))
			{
				anim = new Animation(args.SourceActor.World, info.Image, () => args.Facing);

				if (!string.IsNullOrEmpty(info.OpenSequence))
					anim.PlayThen(info.OpenSequence, () => anim.PlayRepeating(info.Sequences.Random(args.SourceActor.World.SharedRandom)));
				else
					anim.PlayRepeating(info.Sequences.Random(args.SourceActor.World.SharedRandom));
			}
		}

		public void Tick(World world)
		{
			lastPos = pos;
			pos += velocity;
			velocity += acceleration;

			if (!string.IsNullOrEmpty(info.PointDefenseType))
			{
				var shouldExplode = world.ActorsWithTrait<IPointDefense>().Any(x => x.Trait.Destroy(pos, args.SourceActor.Owner, info.PointDefenseType));
				if (shouldExplode)
				{
					var warheadArgs = new WarheadArgs(args)
					{
						ImpactOrientation = new WRot(WAngle.Zero, Util.GetVerticalAngle(lastPos, pos), args.Facing),
						ImpactPosition = pos,
					};

					args.Weapon.Impact(Target.FromPos(pos), warheadArgs);
					world.AddFrameEndTask(w => w.Remove(this));
					return;
				}
			}

			if (pos.Z <= args.PassiveTarget.Z)
			{
				pos += new WVec(0, 0, args.PassiveTarget.Z - pos.Z);
				world.AddFrameEndTask(w => w.Remove(this));

				var warheadArgs = new WarheadArgs(args)
				{
					ImpactOrientation = new WRot(WAngle.Zero, Util.GetVerticalAngle(lastPos, pos), args.Facing),
					ImpactPosition = pos,
				};

				args.Weapon.Impact(Target.FromPos(pos), warheadArgs);
			}

			anim?.Tick();
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (anim == null)
				yield break;

			var world = args.SourceActor.World;
			if (!world.FogObscures(pos))
			{
				if (info.Shadow)
				{
					var dat = world.Map.DistanceAboveTerrain(pos);
					var shadowPos = pos - new WVec(0, 0, dat.Length);
					foreach (var r in anim.Render(shadowPos, wr.Palette(info.ShadowPalette)))
						yield return r;
				}

				var palette = wr.Palette(info.Palette + (info.IsPlayerPalette ? args.SourceActor.Owner.InternalName : ""));
				foreach (var r in anim.Render(pos, palette))
					yield return r;
			}
		}
	}
}
