*note v1.1 allows export to descenders but is semi broken, also requires you to put the editor file into Assets/Editor/ <br>
*note v2 is a full controlled script, put SkiLiftGenerator.cs into Assets/Editor.  put SkiLiftController.cs into Assets/<br>
*Assign SkiLiftController.cs to your generated Ski Lift Object so you can define animation.<br>
Cable renderer is the generated cable object, Chairs parent would be the SkiLift object<br>
* V2 requires your tower model to be parent object, child objects of wheels where wheel0 is first uphill wheel, wheel2 is second uphill wheel, ie name them sequential even numbers<br>
downhill wheels are oppsoite. ie wheel1 is oppsosite of wheel0, wheel3 is opposite of wheel2 etc all odd numbers<br><br><br>

put SkiLiftTower.cs into Assets/<br>
create a new empty object and name it SkiLift<br>
drag SkiLiftTower.cs onto that SkyLift object and define the fields.<br><br>

currently start wheel end wheel start return wheel and return end wheel need to be defined..  start would be where you want your cable to start to draw,<br>
end wheel is to the final tower on the same side of tower for that cable<br>
start return wheel would be a wheel on opposite side ie downhill side of the final tower and end return wheel is downhill side of the starting tower.

Your models must have the wheels as a child object to a parent empty object with normals fixed and geometry set to origin<br>
I have included the original models i downloaded, credit to : by Poly by Google [CC-BY] via Poly Pizza<br><br>
I also included my fbx file to at least seperate 4 of the wheels from this model<br><br>

To see it work you must set up a camera to view your towers and hit the play button to view them working.
