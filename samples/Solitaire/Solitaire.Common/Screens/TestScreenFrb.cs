using FlatRedBall2;
using Solitaire.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solitaire.Screens;

internal class TestScreenFrb : Screen
{
    public override void CustomInitialize()
    {
        this.Add(new TestScreen());

        //var rectEntity = new GumRectEntity { X = -330, Y = 240 };
        //Register(rectEntity);
        //rectEntity.CustomInitialize();

        //base.CustomInitialize();
    }
}
