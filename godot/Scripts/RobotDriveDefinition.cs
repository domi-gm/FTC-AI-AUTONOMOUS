using System;
using System.Collections.Generic;
using System.Linq;

public sealed class RobotDriveWheel
{
    public string JointId {get;set;}="";
    public float RollerSign {get;set;}=1;
    public float TurnSign {get;set;}=1;
    public RobotDriveWheel Copy()=>(RobotDriveWheel)MemberwiseClone();
}
public sealed class RobotDriveDefinition
{
    public float Radius {get;set;}=.055f;
    public float FreeSpeed {get;set;}=49.5f; // rad/s at gearbox output.
    public float StallTorque {get;set;}=1.55f; // Nm at gearbox output, includes estimated efficiency.
    public float Traction {get;set;}=.7f;
    public float SlipStiffness {get;set;}=100; // N s/m, approximate roller contact compliance.
    public List<RobotDriveWheel> Wheels {get;set;}=new();
    public RobotDriveDefinition Copy(){var d=(RobotDriveDefinition)MemberwiseClone();d.Wheels=Wheels.Select(w=>w.Copy()).ToList();return d;}
    public void Validate(ImportedRobotDefinition d)
    {
        if(!float.IsFinite(Radius+FreeSpeed+StallTorque+Traction+SlipStiffness) || Radius<.01f || Radius>.3f || FreeSpeed<=0 || FreeSpeed>1000 || StallTorque<=0 || StallTorque>100 || Traction<=0 || Traction>2 || SlipStiffness<=0 || SlipStiffness>10000 || Wheels?.Count!=4)
            throw new ArgumentException("Invalid mecanum drive parameters.");
        var ids=new HashSet<string>();
        foreach(var w in Wheels)
            if(w==null || !ids.Add(w.JointId) || Math.Abs(w.RollerSign)!=1 || Math.Abs(w.TurnSign)!=1 || !d.Joints.Any(j=>j.Id==w.JointId && j.Type=="revolute" && !j.LimitsEnabled && j.ParentBody=="chassis"))
                throw new ArgumentException("Mecanum drive requires four distinct continuous wheel joints attached to chassis.");
    }
}
