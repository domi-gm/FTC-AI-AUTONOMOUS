using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>SI inertia about COM, expressed in the shared source frame.</summary>
public static class RobotMassProperties
{
    public static Dictionary<string,string> BodyRoots(ImportedRobotDefinition d)
    {
        var parents=d.Joints.Where(j=>j.Type=="fixed").ToDictionary(j=>j.ChildBody,j=>j.ParentBody);
        string Root(string id)=>d.PhysicalDrive!=null && parents.TryGetValue(id,out var p) ? Root(p) : id;
        return d.Bodies.ToDictionary(b=>b.Id,b=>Root(b.Id));
    }
    public static (Basis Axes, Vector3 Center,Vector3 Moments,float Mass) Compound(ImportedRobotDefinition d,RobotBodyDefinition[] bodies)
    {
        var conversion=new Basis(ImportedRobot.ConvertPoint(Vector3.Right,d).Normalized(),ImportedRobot.ConvertPoint(Vector3.Up,d).Normalized(),ImportedRobot.ConvertPoint(Vector3.Back,d).Normalized());
        var offset=ImportedRobot.NormalizationOffset(d);
        float mass=bodies.Sum(b=>b.MassKg);var center=Vector3.Zero;
        var centers=new Dictionary<string,Vector3>();var tensors=new Dictionary<string,float[]>();
        foreach(var b in bodies)
        {
            var bounds=ImportedRobot.GetBodyBounds(d,b.Id);
            var c=b.CenterOfMassSource==null ? bounds.GetCenter() : ImportedRobot.ConvertPoint(RobotMechanismValidation.Vec(b.CenterOfMassSource),d)+offset;
            centers[b.Id]=c;center+=c*b.MassKg;
            var size=bounds.Size;
            tensors[b.Id]=b.InertiaSource==null ? new[]{b.MassKg*(size.Y*size.Y+size.Z*size.Z)/12,b.MassKg*(size.X*size.X+size.Z*size.Z)/12,b.MassKg*(size.X*size.X+size.Y*size.Y)/12,0,0,0} : Tensor(conversion,b.InertiaSource);
        }
        center/=mass;var total=new float[6];
        foreach(var b in bodies)
        {
            var v=centers[b.Id]-center;
            var shift=new[]{v.Y*v.Y+v.Z*v.Z,v.X*v.X+v.Z*v.Z,v.X*v.X+v.Y*v.Y,-v.X*v.Y,-v.X*v.Z,-v.Y*v.Z};
            for(int i=0;i<6;i++)total[i]+=tensors[b.Id][i]+b.MassKg*shift[i];
        }
        var principal=Principal(total);return (principal.Axes,center,principal.Moments,mass);
    }
    public static (Basis Axes, Vector3 Moments) Principal(float[] tensor)
    {
        if (tensor?.Length != 6) throw new ArgumentException("Inertia requires xx, yy, zz, xy, xz, yz.");
        double[,] a = {{tensor[0],tensor[3],tensor[4]}, {tensor[3],tensor[1],tensor[5]}, {tensor[4],tensor[5],tensor[2]}};
        double[,] v = {{1,0,0},{0,1,0},{0,0,1}};
        foreach (float value in tensor) if (!float.IsFinite(value)) throw new ArgumentException("Non-finite inertia.");
        for (int iteration=0; iteration<32; iteration++)
        {
            int p=0,q=1;
            if (Math.Abs(a[0,2])>Math.Abs(a[p,q])) {p=0;q=2;}
            if (Math.Abs(a[1,2])>Math.Abs(a[p,q])) {p=1;q=2;}
            if (Math.Abs(a[p,q]) < Math.Max(1e-15, (Math.Abs(a[0,0])+Math.Abs(a[1,1])+Math.Abs(a[2,2]))*1e-12)) break;
            double angle=.5*Math.Atan2(2*a[p,q],a[q,q]-a[p,p]), c=Math.Cos(angle),s=Math.Sin(angle);
            double pp=a[p,p],qq=a[q,q],pq=a[p,q];
            for(int k=0;k<3;k++) if(k!=p && k!=q)
            {double kp=a[k,p],kq=a[k,q];a[k,p]=a[p,k]=c*kp-s*kq;a[k,q]=a[q,k]=s*kp+c*kq;}
            a[p,p]=c*c*pp-2*s*c*pq+s*s*qq;a[q,q]=s*s*pp+2*s*c*pq+c*c*qq;a[p,q]=a[q,p]=0;
            for(int k=0;k<3;k++) {double kp=v[k,p],kq=v[k,q];v[k,p]=c*kp-s*kq;v[k,q]=s*kp+c*kq;}
        }
        var moments=new Vector3((float)a[0,0],(float)a[1,1],(float)a[2,2]);
        float sum=moments.X+moments.Y+moments.Z;
        if (moments.X<=0 || moments.Y<=0 || moments.Z<=0 || 2*Mathf.Max(moments.X,Mathf.Max(moments.Y,moments.Z))>sum*1.0001f)
            throw new ArgumentException("Inertia must be positive and satisfy the triangle inequality.");
        var axes=new Basis(new((float)v[0,0],(float)v[1,0],(float)v[2,0]),new((float)v[0,1],(float)v[1,1],(float)v[2,1]),new((float)v[0,2],(float)v[1,2],(float)v[2,2]));
        if(axes.Determinant()<0) axes.Z=-axes.Z;
        return (axes,moments);
    }
    public static float[] Tensor(Basis axes, float[] values)
    {
        var m=new Basis(new(values[0],values[3],values[4]),new(values[3],values[1],values[5]),new(values[4],values[5],values[2]));
        var t=axes*m*axes.Transposed();
        return new[]{t.X.X,t.Y.Y,t.Z.Z,t.Y.X,t.Z.X,t.Z.Y};
    }
}
