using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Dynamic links attached to the existing kinematic drive chassis.</summary>
public partial class ImportedRobotRig : Node3D
{
    public RobotAgent Actor;
    public ImportedRobotDefinition Definition;
    public readonly Dictionary<string, RigidBody3D> Links = new();
    public readonly Dictionary<string, Joint3D> Connections = new();
    public RigidBody3D PhysicalChassis { get; private set; }
    public readonly Dictionary<string,(float Power,float Omega,float Force,bool Contact)> DriveDiagnostics=new();
    private Dictionary<string,string> _bodyRoots;
    private IEnumerable<KeyValuePair<string,RigidBody3D>> AllBodies => PhysicalChassis==null ? Links : Links.Concat(new[]{new KeyValuePair<string,RigidBody3D>("chassis",PhysicalChassis)}).Where(p=>_bodyRoots[p.Key]==p.Key);
    private PhysicsBody3D ParentBody(string id) => id=="chassis" ? (PhysicsBody3D)PhysicalChassis ?? Actor : Links[id];
    public void SyncPhysicalActor()
    {
        if(PhysicalChassis==null) return;
        Actor.GlobalTransform=PhysicalChassis.GlobalTransform*_bodyRest["chassis"].AffineInverse();
        Actor.Velocity=PhysicalChassis.LinearVelocity;
    }
    public void StepPhysicalDrive()
    {
        SyncPhysicalActor();var drive=Definition.PhysicalDrive;if(drive==null || !Actor.Game.DrivingAllowed || Actor.Game.Hud.MenuVisible) return;
        var local=Actor.GlobalBasis.Inverse()*Actor.Command.LimitLength();
        float forward=-local.Z,strafe=local.X,turn=Actor.TurnCommand;
        float denominator=Mathf.Max(1,Mathf.Abs(forward)+Mathf.Abs(strafe)+Mathf.Abs(turn));
        float weight=Definition.Bodies.Sum(b=>b.MassKg)*9.81f/4;
        var up=Actor.GlobalBasis.Y;
        foreach(var wheel in drive.Wheels)
        {
            var spec=Definition.Joints.First(j=>j.Id==wheel.JointId);var child=Links[spec.ChildBody];
            var axis=PhysicalChassis.GlobalBasis*_axes[spec.Id];
            float omega=(child.AngularVelocity-PhysicalChassis.AngularVelocity).Dot(axis);
            float power=(forward+wheel.RollerSign*strafe+wheel.TurnSign*turn)/denominator;
            float torque=drive.StallTorque*Mathf.Clamp(power-omega/drive.FreeSpeed,-1,1);
            child.ApplyTorque(axis*torque);PhysicalChassis.ApplyTorque(-axis*torque);
            DriveDiagnostics[spec.Name]=(power,omega,0,false);
            var pivot=child.GlobalTransform*_childPivots[spec.Id];
            var query=PhysicsRayQueryParameters3D.Create(pivot+up*.02f,pivot-up*(drive.Radius+.035f),1);
            query.Exclude=new Godot.Collections.Array<Rid>(AllBodies.Select(b=>b.Value.GetRid()));
            if(GetWorld3D().DirectSpaceState.IntersectRay(query).Count==0) continue;
            var direction=(Actor.Front+Actor.GlobalBasis.X*wheel.RollerSign).Normalized();
            var contact=pivot-up*drive.Radius;
            var speed=(child.LinearVelocity+child.AngularVelocity.Cross(pivot-child.GlobalPosition)).Dot(direction);
            float slip=omega*drive.Radius/Mathf.Sqrt(2)-speed;
            float force=Mathf.Clamp(drive.SlipStiffness*slip,-drive.Traction*weight,drive.Traction*weight);
            DriveDiagnostics[spec.Name]=(power,omega,force,true);
            child.ApplyForce(direction*force,contact-child.GlobalPosition);
        }
    }
    private readonly Dictionary<string, (Vector3 Linear, Vector3 Angular)> _paused = new();
    private readonly Dictionary<string, float> _commands = new();
    private readonly Dictionary<string, float> _targets = new();
    private readonly Dictionary<string, Vector3> _restOffsets = new(), _axes = new();
    private readonly Dictionary<string, Basis> _restRotations = new();
    private readonly Dictionary<string, Transform3D> _bodyRest = new();
    private readonly Dictionary<string,Vector3> _childPivots = new();
    private int _selected;
    private RobotJointDefinition[] _motors = Array.Empty<RobotJointDefinition>();
    private readonly Dictionary<string, (bool Enabled, float Speed, float Step)> _lastMotor = new();
    public string MotorStatus => _motors.Length == 0 ? "No joint motors configured" : "Joint: " + _motors[_selected % _motors.Length].Name + "   [I] next   [U/O] move";
    public void SelectNextMotor() { _selected++; _commands.Clear(); }
    public void SetMotorCommand(string id, float command)
    {
        if (!float.IsFinite(command) || !Definition.Joints.Any(j => j.Id == id && j.MotorEnabled)) throw new ArgumentException("Unknown motor / invalid command");
        _commands[id] = Mathf.Clamp(command, -1, 1);
        _targets.Remove(id);
    }
    public float JointPosition(string id)
    {
        var spec = Definition.Joints.First(j => j.Id == id);
        PhysicsBody3D parent = ParentBody(spec.ParentBody);
        var relative = parent.GlobalTransform.AffineInverse() * Links[spec.ChildBody].GlobalTransform;
        float position;
        if (spec.Type == "prismatic") position = (relative.Origin - _restOffsets[id]).Dot(_axes[id]);
        else
        {
            var q = (relative.Basis * _restRotations[id].Inverse()).Orthonormalized().GetRotationQuaternion();
            position = Mathf.Wrap(2 * Mathf.Atan2(new Vector3(q.X,q.Y,q.Z).Dot(_axes[id]), q.W), -Mathf.Pi, Mathf.Pi);
        }
        return position + spec.ReferencePosition;
    }
    public bool NextToggleOpens(string id)
    {
        var spec = Definition.Joints.First(j => j.Id == id);
        float position = _targets.TryGetValue(id, out float target) ? target : JointPosition(id);
        return Mathf.Abs(position - spec.ClosedPosition) <= Mathf.Abs(position - spec.OpenPosition);
    }
    public bool ToggleJoint(string id)
    {
        var spec = Definition.Joints.FirstOrDefault(j => j.Id == id);
        if (spec?.ToggleEnabled != true || !Actor.Game.DrivingAllowed || Actor.Game.Hud.MenuVisible) return false;
        _targets[id] = NextToggleOpens(id) ? spec.OpenPosition : spec.ClosedPosition;
        _commands.Remove(id); return true;
    }
    public bool HandleToggleKey(InputEventKey input)
    {
        if (!input.Pressed || input.Echo || input.CtrlPressed || input.AltPressed || input.MetaPressed || input.ShiftPressed) return false;
        var key = input.PhysicalKeycode.ToString();
        var spec = Definition.Joints.FirstOrDefault(j => j.ToggleEnabled && j.ToggleKey != "" && j.ToggleKey == key);
        return spec != null && ToggleJoint(spec.Id);
    }
    public static Basis AxisFrame(Vector3 axis, bool hinge)
    {
        axis = axis.Normalized();
        var reference = Mathf.Abs(axis.Dot(Vector3.Up)) > .9f ? Vector3.Right : Vector3.Up;
        if (hinge) { var x = reference.Cross(axis).Normalized(); return new(x, axis.Cross(x).Normalized(), axis); }
        var z = axis.Cross(reference).Normalized(); return new(axis, z.Cross(axis).Normalized(), z);
    }
    public override void _Ready()
    {
        RobotMechanismValidation.ValidateReady(Definition);
        _motors = Definition.Joints.Where(j => j.MotorEnabled && j.Type != "fixed" && Definition.PhysicalDrive?.Wheels.Any(w=>w.JointId==j.Id)!=true).ToArray();
        var visual = ImportedRobot.Build(Definition);
        AddChild(visual);
        _bodyRoots=RobotMassProperties.BodyRoots(Definition);
        foreach (var spec in Definition.Bodies.Where(b => _bodyRoots[b.Id]==b.Id && (b.Id != "chassis" || Definition.PhysicalDrive!=null)))
        {
            var bounds = ImportedRobot.GetBodyBounds(Definition, spec.Id);
            Basis basis=Basis.Identity; Vector3 center=bounds.GetCenter(), inertia=Vector3.Zero;
            if(spec.InertiaSource != null)
            {
                var principal=RobotMassProperties.Principal(spec.InertiaSource);
                basis=new Basis(ImportedRobot.ConvertPoint(principal.Axes.X,Definition).Normalized(),ImportedRobot.ConvertPoint(principal.Axes.Y,Definition).Normalized(),ImportedRobot.ConvertPoint(principal.Axes.Z,Definition).Normalized());
                center=ImportedRobot.ConvertPoint(RobotMechanismValidation.Vec(spec.CenterOfMassSource),Definition)+ImportedRobot.NormalizationOffset(Definition);
                inertia=principal.Moments;
            }
            float mass=spec.MassKg;
            if(Definition.PhysicalDrive!=null)
            {
                var aggregate=RobotMassProperties.Compound(Definition,Definition.Bodies.Where(b=>_bodyRoots[b.Id]==spec.Id).ToArray());
                basis=aggregate.Axes;center=aggregate.Center;inertia=aggregate.Moments;mass=aggregate.Mass;
            }
            var rest=new Transform3D(basis,center);_bodyRest[spec.Id]=rest;
            var body = new RigidBody3D { Name = "Body_" + spec.Id, TopLevel = true, Mass = mass,
                CenterOfMassMode=RigidBody3D.CenterOfMassModeEnum.Custom,CenterOfMass=Vector3.Zero,Inertia=inertia,
                CollisionLayer = 4, CollisionMask = 1 | 2 | 4, Freeze = true,
                PhysicsMaterialOverride = new PhysicsMaterial { Friction = spec.Friction, Bounce = 0 }, CanSleep = false };
            AddChild(body); body.GlobalTransform = Actor.GlobalTransform * rest;
            var wheelJoint=Definition.PhysicalDrive==null ? null : Definition.Joints.FirstOrDefault(j=>j.ChildBody==spec.Id && Definition.PhysicalDrive.Wheels.Any(w=>w.JointId==j.Id));
            if(wheelJoint != null)
            {
                var axis=ImportedRobot.ConvertPoint(RobotMechanismValidation.Vec(wheelJoint.AxisSource),Definition).Normalized();
                var reference=Mathf.Abs(axis.Dot(Vector3.Up))>.9f ? Vector3.Right : Vector3.Up;
                var x=axis.Cross(reference).Normalized();var cylinderBasis=new Basis(x,axis,x.Cross(axis));
                var pivot=ImportedRobot.ConvertPoint(RobotMechanismValidation.Vec(wheelJoint.PivotSource),Definition)+ImportedRobot.NormalizationOffset(Definition);
                body.PhysicsMaterialOverride.Friction=0; // Roller contact is computed by the drive model.
                body.AddChild(new CollisionShape3D{Transform=rest.AffineInverse()*new Transform3D(cylinderBasis,pivot),Shape=new CylinderShape3D{Radius=Definition.PhysicalDrive.Radius,Height=bounds.Size.Dot(axis.Abs())}});
            }
            else
            {
                // Boxes contain empty CAD space. A 2 mm skin avoids phantom floor
                // contact by thin intake blades while keeping ground collision active.
                var size=Definition.PhysicalDrive==null ? bounds.Size : new Vector3(Mathf.Max(.001f,bounds.Size.X-.004f),Mathf.Max(.001f,bounds.Size.Y-.004f),Mathf.Max(.001f,bounds.Size.Z-.004f));
                body.AddChild(new CollisionShape3D { Transform=rest.AffineInverse()*new Transform3D(Basis.Identity,bounds.GetCenter()), Shape = new BoxShape3D { Size = size } });
            }
            if(spec.Id=="chassis") PhysicalChassis=body;else Links.Add(spec.Id, body);
        }
        if(PhysicalChassis!=null)
        foreach(var spec in Definition.Bodies.Where(b=>_bodyRoots[b.Id]!=b.Id))
        {
            var canonical=_bodyRoots[spec.Id];var body=canonical=="chassis" ? PhysicalChassis : Links[canonical];
            Links[spec.Id]=body;_bodyRest[spec.Id]=_bodyRest[canonical];
            var bounds=ImportedRobot.GetBodyBounds(Definition,spec.Id);
            var size=new Vector3(Mathf.Max(.001f,bounds.Size.X-.004f),Mathf.Max(.001f,bounds.Size.Y-.004f),Mathf.Max(.001f,bounds.Size.Z-.004f));
            body.AddChild(new CollisionShape3D{Transform=_bodyRest[canonical].AffineInverse()*new Transform3D(Basis.Identity,bounds.GetCenter()),Shape=new BoxShape3D{Size=size}});
        }
        for (int i = 0; i < Definition.Parts.Count; i++)
        {
            var part = Definition.Parts[i]; if (part.BodyId == "chassis" && PhysicalChassis==null) continue;
            var mesh = visual.GetNode<Node3D>("RigidGroup" + i); visual.RemoveChild(mesh);
            (part.BodyId=="chassis" ? PhysicalChassis : Links[part.BodyId]).AddChild(mesh); mesh.Transform = _bodyRest[part.BodyId].AffineInverse();
        }
        foreach (var body in AllBodies.Select(b=>b.Value))
        {
            body.AddCollisionExceptionWith(Actor); Actor.AddCollisionExceptionWith(body);
            foreach (var other in AllBodies.Select(b=>b.Value)) if (body != other) body.AddCollisionExceptionWith(other);
        }
        var offset = ImportedRobot.NormalizationOffset(Definition);
        foreach (var spec in Definition.Joints)
        {
            if(PhysicalChassis!=null && spec.Type=="fixed") continue;
            Joint3D joint;
            if (spec.Type == "revolute")
            {
                var hinge = new HingeJoint3D();
                hinge.SetFlag(HingeJoint3D.Flag.UseLimit, spec.LimitsEnabled);
                // Godot hinges measure clockwise angles; the CAD/preview convention is right-handed.
                hinge.SetParam(HingeJoint3D.Param.LimitLower, -spec.Upper); hinge.SetParam(HingeJoint3D.Param.LimitUpper, -spec.Lower);
                joint = hinge;
            }
            else
            {
                var slide = new Generic6DofJoint3D();
                slide.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearLimit, spec.Type == "fixed" || spec.LimitsEnabled);
                slide.SetParamX(Generic6DofJoint3D.Param.LinearLowerLimit, spec.Type == "fixed" ? 0 : spec.Lower);
                slide.SetParamX(Generic6DofJoint3D.Param.LinearUpperLimit, spec.Type == "fixed" ? 0 : spec.Upper);
                slide.SetFlagY(Generic6DofJoint3D.Flag.EnableLinearLimit, true); slide.SetFlagZ(Generic6DofJoint3D.Flag.EnableLinearLimit, true);
                slide.SetParamY(Generic6DofJoint3D.Param.LinearLowerLimit, 0); slide.SetParamY(Generic6DofJoint3D.Param.LinearUpperLimit, 0);
                slide.SetParamZ(Generic6DofJoint3D.Param.LinearLowerLimit, 0); slide.SetParamZ(Generic6DofJoint3D.Param.LinearUpperLimit, 0);
                slide.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularLimit, true); slide.SetFlagY(Generic6DofJoint3D.Flag.EnableAngularLimit, true); slide.SetFlagZ(Generic6DofJoint3D.Flag.EnableAngularLimit, true);
                slide.SetParamX(Generic6DofJoint3D.Param.AngularLowerLimit, 0); slide.SetParamX(Generic6DofJoint3D.Param.AngularUpperLimit, 0);
                slide.SetParamY(Generic6DofJoint3D.Param.AngularLowerLimit, 0); slide.SetParamY(Generic6DofJoint3D.Param.AngularUpperLimit, 0);
                slide.SetParamZ(Generic6DofJoint3D.Param.AngularLowerLimit, 0); slide.SetParamZ(Generic6DofJoint3D.Param.AngularUpperLimit, 0);
                joint = slide;
            }
            joint.Name = "Joint_" + spec.Id; joint.ExcludeNodesFromCollision = true;
            // Store dynamic anchors in the actual bodies, without re-anchoring
            // every time the CharacterBody view follows the physical chassis.
            joint.TopLevel=PhysicalChassis!=null;
            AddChild(joint);
            var axis = ImportedRobot.ConvertPoint(RobotMechanismValidation.Vec(spec.AxisSource), Definition).Normalized();
            var pivot = ImportedRobot.ConvertPoint(RobotMechanismValidation.Vec(spec.PivotSource), Definition) + offset;
            _childPivots[spec.Id]=_bodyRest[spec.ChildBody].AffineInverse()*pivot;
            joint.GlobalTransform = Actor.GlobalTransform * new Transform3D(AxisFrame(axis, spec.Type == "revolute"), pivot);
            PhysicsBody3D parent = ParentBody(spec.ParentBody);
            _axes[spec.Id] = parent.GlobalBasis.Inverse()*Actor.GlobalBasis*axis;
            _restOffsets[spec.Id] = parent.GlobalTransform.AffineInverse() * Links[spec.ChildBody].GlobalPosition;
            _restRotations[spec.Id] = parent.GlobalBasis.Inverse()*Links[spec.ChildBody].GlobalBasis;
            joint.NodeA = joint.GetPathTo(parent); joint.NodeB = joint.GetPathTo(Links[spec.ChildBody]);
            Connections.Add(spec.Id, joint);
        }
        if(PhysicalChassis!=null) {Actor.CollisionLayer=0;Actor.CollisionMask=0;}
        foreach (var body in AllBodies.Select(b=>b.Value)) body.Freeze = false;
    }
    public override void _PhysicsProcess(double delta)
    {
        foreach(var spec in Definition.Joints.Where(j=>j.Type!="fixed" && (j.Damping>0 || j.JointFriction>0)))
        {
            if(!Actor.Game.DrivingAllowed || Actor.Game.Hud.MenuVisible) continue;
            PhysicsBody3D parent=ParentBody(spec.ParentBody);
            var child=Links[spec.ChildBody];var axis=parent.GlobalBasis*_axes[spec.Id];
            var parentVelocity=parent is RigidBody3D rigid ? (spec.Type=="prismatic" ? rigid.LinearVelocity : rigid.AngularVelocity) : (spec.Type=="prismatic" ? Actor.Velocity : Vector3.Up*Actor.TurnCommand*Actor.TurnSpeed);
            float speed=((spec.Type=="prismatic" ? child.LinearVelocity : child.AngularVelocity)-parentVelocity).Dot(axis);
            float resistance=spec.Damping*speed+spec.JointFriction*Mathf.Clamp(speed/.01f,-1,1);
            if(spec.Type=="prismatic") child.ApplyCentralForce(-axis*resistance);else child.ApplyTorque(-axis*resistance);
            if(parent is RigidBody3D reaction) {if(spec.Type=="prismatic") reaction.ApplyCentralForce(axis*resistance);else reaction.ApplyTorque(axis*resistance);}
        }
        foreach (var spec in _motors)
        {
            bool enabled = spec.MotorEnabled && Actor.Game.DrivingAllowed && !Actor.Game.Hud.MenuVisible;
            float command = _commands.GetValueOrDefault(spec.Id);
            if (spec.IntakePower != 0) command = Actor.Intake ? spec.IntakePower : 0;
            if (spec == _motors[_selected % _motors.Length] && !Actor.Game.Testing)
            {
                float manual = (Input.IsPhysicalKeyPressed(Key.O) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.U) ? 1 : 0);
                if (manual != 0) { command = manual; _targets.Remove(spec.Id); } // Holding manual movement takes over from a position button.
            }
            float speed = enabled ? command * spec.MaxSpeed : 0;
            if (enabled && _targets.TryGetValue(spec.Id, out float target))
            {
                float error = target - JointPosition(spec.Id);
                speed = Mathf.Abs(error) < (spec.Type == "prismatic" ? .0003f : .001f) ? 0 : Mathf.Clamp(error * 8, -spec.MaxSpeed, spec.MaxSpeed);
            }
            var state = (enabled, speed, (float)delta);
            if (_lastMotor.TryGetValue(spec.Id, out var previous) && previous == state) continue;
            _lastMotor[spec.Id] = state;
            if (Connections[spec.Id] is HingeJoint3D hinge)
            {
                hinge.SetFlag(HingeJoint3D.Flag.EnableMotor, enabled);
                // Godot hinge impulses are torque multiplied by the current physics step.
                hinge.SetParam(HingeJoint3D.Param.MotorMaxImpulse, spec.MaxEffort * (float)delta);
                hinge.SetParam(HingeJoint3D.Param.MotorTargetVelocity, -speed);
            }
            else if (Connections[spec.Id] is Generic6DofJoint3D slide)
            {
                slide.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, enabled && spec.Type == "prismatic");
                slide.SetParamX(Generic6DofJoint3D.Param.LinearMotorForceLimit, spec.MaxEffort);
                slide.SetParamX(Generic6DofJoint3D.Param.LinearMotorTargetVelocity, speed);
            }
        }
    }
    public void SetFrozen(bool frozen)
    {
        foreach (var pair in AllBodies)
        {
            var body = pair.Value;
            if (frozen && !_paused.ContainsKey(pair.Key)) _paused[pair.Key] = (body.LinearVelocity, body.AngularVelocity);
            body.Freeze = frozen;
            if (!frozen && _paused.Remove(pair.Key, out var motion)) { body.LinearVelocity = motion.Linear; body.AngularVelocity = motion.Angular; }
        }
    }
    public RobotLinkState[] Capture() => AllBodies.Select(pair =>
    {
        var local = pair.Key=="chassis" ? pair.Value.GlobalTransform : Actor.GlobalTransform.AffineInverse() * pair.Value.GlobalTransform;
        var q = local.Basis.GetRotationQuaternion(); var p = local.Origin;
        var motion = _paused.TryGetValue(pair.Key, out var saved) ? saved : (pair.Value.LinearVelocity, pair.Value.AngularVelocity);
        return new RobotLinkState { BodyId = pair.Key, Position = new[] { p.X, p.Y, p.Z }, Rotation = new[] { q.X, q.Y, q.Z, q.W },
            TargetPosition = Definition.Joints.Where(j => j.ChildBody == pair.Key).Select(j => _targets.TryGetValue(j.Id, out float target) ? (float?)target : null).FirstOrDefault(),
            Linear = new[] { motion.Item1.X, motion.Item1.Y, motion.Item1.Z }, Angular = new[] { motion.Item2.X, motion.Item2.Y, motion.Item2.Z } };
    }).ToArray();
    public void Restore(RobotLinkState[] states)
    {
        RobotLinkState.Validate(states, Definition);
        _targets.Clear(); _commands.Clear();
        if(PhysicalChassis != null)
        {
            var state=states?.First(s=>s.BodyId=="chassis");
            PhysicalChassis.GlobalTransform=state==null ? Actor.GlobalTransform*_bodyRest["chassis"] : new Transform3D(new Basis(new Quaternion(state.Rotation[0],state.Rotation[1],state.Rotation[2],state.Rotation[3]).Normalized()),RobotMechanismValidation.Vec(state.Position));
            PhysicalChassis.LinearVelocity=state==null ? Vector3.Zero : RobotMechanismValidation.Vec(state.Linear);
            PhysicalChassis.AngularVelocity=state==null ? Vector3.Zero : RobotMechanismValidation.Vec(state.Angular);
            SyncPhysicalActor();
        }
        foreach (var pair in Links.Where(p=>_bodyRoots[p.Key]==p.Key))
        {
            var body = pair.Value; var state = states?.First(s => s.BodyId == pair.Key);
            body.GlobalTransform = Actor.GlobalTransform * (state == null
                ? _bodyRest[pair.Key]
                : new Transform3D(new Basis(new Quaternion(state.Rotation[0], state.Rotation[1], state.Rotation[2], state.Rotation[3]).Normalized()), RobotMechanismValidation.Vec(state.Position)));
            body.LinearVelocity = state == null ? Vector3.Zero : RobotMechanismValidation.Vec(state.Linear);
            body.AngularVelocity = state == null ? Vector3.Zero : RobotMechanismValidation.Vec(state.Angular);
            if (state?.TargetPosition is float target) _targets[Definition.Joints.First(j => j.ChildBody == pair.Key).Id] = target;
        }
    }
}

public sealed class RobotLinkState
{
    public string BodyId { get; set; }
    public float[] Position { get; set; }
    public float[] Rotation { get; set; }
    public float[] Linear { get; set; }
    public float[] Angular { get; set; }
    public float? TargetPosition { get; set; }
    public static void Validate(RobotLinkState[] states, ImportedRobotDefinition definition)
    {
        if (states == null) return; // Older snapshots restore the imported rest pose.
        var ids = definition==null ? new HashSet<string>() : RobotMassProperties.BodyRoots(definition).Values.Where(id=>id!="chassis" || definition.PhysicalDrive!=null).ToHashSet();
        if (states.Length != ids.Count) throw new ArgumentException("Incomplete mechanism snapshot");
        foreach (var state in states)
        {
            if (state == null || !ids.Remove(state.BodyId) || !RobotMechanismValidation.Vector(state.Position)
                || RobotMechanismValidation.Vec(state.Position).Length() > 30 || !RobotMechanismValidation.Vector(state.Linear)
                || !RobotMechanismValidation.Vector(state.Angular) || state.Rotation?.Length != 4 || !state.Rotation.All(float.IsFinite)
                || Math.Abs(state.Rotation.Sum(v => v * v) - 1) > .01f) throw new ArgumentException("Invalid mechanism snapshot");
            if (state.TargetPosition is float target)
            {
                var joint = definition.Joints.FirstOrDefault(j => j.ChildBody == state.BodyId);
                if (joint == null || !joint.ToggleEnabled || !RobotMechanismValidation.TargetInRange(joint, target)) throw new ArgumentException("Invalid joint target in snapshot");
            }
        }
    }
}
