﻿module Intelligence 
  open TreatzGame
  open CommonData
  open Behaviours  
  open SDLUtility
  open System

  let wanderDefault = { 
                      BehaviourState.RateOfChangeOfDirection = 0.1; 
                      BehaviourState.CircleDistance = 1.00 ;  
                      BehaviourState.CircleRadius = 2.50 ; 
                      BehaviourState.WanderingAngle = 0.10; 
                      SteeringDirection = Vector2.Zero }  

  let intelligence (state: TreatzState) =
      
      let findClosestTreat (mikishida:Mikishida) =
            let clamp x = if x < 0 then 0 else x
            let r = mikishida.AsQuadBounds
            let bounds = {r with x = clamp r.x - 950; y = clamp r.y - 950; width = 9100; height = 9100; }
//            let bounds = {r with x = clamp r.x - 150; y = clamp r.y - 150; width = 300; height = 300; }
            
            state.findMikishidas(fun m -> match m.kind with Treat -> true | _ -> false) bounds
            |> function
                | [] ->                 
                    Console.WriteLine("No treats found, wander")     
                    None
                | treats -> // find the cloest treat and head towards it
                    let treat = List.minBy(mikishida.ManhattanDistance) treats
                    match treat.kind with 
                    | Treat -> Some(treat.location)
                    | _ -> None
                        
      let update mikishida =                    
          match mikishida.kind with                      

          | Dragon(Wander behaviourState)  ->      

              let newBehaviourState = wander state.Chaos mikishida behaviourState 
              let velocity = mikishida.kind.defaultSpeed * newBehaviourState.SteeringDirection.normalize       
              match findClosestTreat mikishida with
              | Some treat -> {mikishida with kind = Dragon(PathFind treat); }
              | _ -> {mikishida with kind = Dragon(Wander newBehaviourState); velocity = velocity}

          | Dragon(FollowPath(pathTo, dest))  ->   
              // if treat has gone, do something else              
                if pathTo.Length > 0  &&  Set.contains (dest.GridX, dest.GridY) state.TreatsLookup then
                    let destinationCell = Array.head pathTo
                    // move towards the cetre of the destination cell, check if we are there yet by looking 
                    // where the centre of the dragon is.
                    let destinationCentre = 
                        (double destinationCell.X) * cellWidthf + (cellWidthf / 2.0),
                        (double destinationCell.Y) * cellHeightf + (cellHeightf / 2.0)
                
                    let dragonCentre = 
                        (double mikishida.location.X + (cellWidthf / 2.0)) ,
                        (double mikishida.location.Y + (cellHeightf / 2.0))
                
                    let destinationCell = int (fst destinationCentre / cellWidthf), int (snd destinationCentre / cellHeightf)
                    let dragonCell = int (fst dragonCentre / cellWidthf), int (snd dragonCentre / cellHeightf)

                    if destinationCell = dragonCell then 
                      {mikishida with kind = Dragon(FollowPath (pathTo |> Array.tail,dest)) }
                    else                        
                        let target = {Vector2.X = fst destinationCentre - fst dragonCentre ; Y = snd destinationCentre - snd dragonCentre}.normalize
                        let velocity = mikishida.kind.defaultSpeed * target
                        { mikishida with velocity = velocity}
                        
                
                else {mikishida with kind = Dragon(Wander wanderDefault); }              
              
          | Dragon(PathFind( treatLocation)) ->              
              
              let rec getNode loc =
                let r = Map.tryFind loc state.PathFindingData                    
                match r with
                | Some node-> Some(node)
                | None  -> 
                      let x, y = loc
                      let x', y' = state.Chaos.Next(-1,2), state.Chaos.Next(-1,2)
                      getNode (x+x', y+y') 
                
              let destinationNode = getNode (treatLocation.GridX,treatLocation.GridY) 
              let origin = getNode(mikishida.location.GridX,mikishida.location.GridY) 
              
              match destinationNode, origin with
              | Some destination, Some origin ->
                  let gridPath = [| yield! PathFinding.search origin destination [state.Player1; state.Player2 ]; yield destination |]
                  
                  printfn "Path  %A \n destination %A" (gridPath |> Array.map(fun x -> x.Identity.X.ToString() + " " + x.Identity.Y.ToString())) (Array.last gridPath).Identity
                  printfn "Find the path from %A to %A" origin.Identity destination.Identity

                  let points = gridPath |> Array.map(fun x -> 
                                        { SDLGeometry.Point.X = (x.Identity.X * cellWidth + cellWidth / 2 ) * 1<px>
                                          SDLGeometry.Point.Y =  (x.Identity.Y * cellHeight + cellHeight/ 2) * 1<px> })
                  
                  state.DebugLines <- points
                  

                  {mikishida with kind = Dragon(FollowPath(gridPath |> Array.map(fun x -> x.Identity), treatLocation))}
              |  _ -> printfn "this should never happen, wtf"
                      {mikishida with kind = Dragon(Wander wanderDefault)}
          
          | _ -> mikishida

      { state with Mikishidas = List.map update state.Mikishidas }
