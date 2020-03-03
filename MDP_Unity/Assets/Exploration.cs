using System;
using System.Threading;
using System.Timers;
using UnityEngine;
using static Arena;
using static Algo;
using System.Collections;
using System.Collections.Generic;
public class Exploration {
    private static System.Timers.Timer endTimer;
    static bool _testing;
    static bool _usingRightHandWall = true;
    static bool _underSixMinutes = true;
    public static bool _hasNewSensorData = false;

    static GridStatus[,] _testBoard;
    static Mutex _bufferMutex = new Mutex();
    public static string _sensorBuffer;
    public static Instruction calibrateInstru = Instruction.Stop;
    public static int STEPS_EVERY_CALIBRATE = 4;
    static int rightWallCounter = STEPS_EVERY_CALIBRATE;
    public static int overallCalibrateCounter = STEPS_EVERY_CALIBRATE * 2;

    static int _lastExploredCount = 0;
    static int _currExploredCount;
    static int countUsingRHWSteps = 0;
    public static int totalSteps = 0;
    static Pos[] lastPoses = new Pos[8];
    static Direction[] lastDirs = new Direction[8];
    static HashSet<string> visitedEdges = new HashSet<string>();
    static List<string> notYetTakenSurfaces = new List<string>();

    #region helper methods 	

    public static bool CanCalibratedUsingFrontWall() {
        try {
            if (
                //BlockFree(
                //currentPos.x + 2 * DirectionToVector(currentDir).x + DirectionToVector(LeftDirection()).x,
                //currentPos.y + 2 * DirectionToVector(currentDir).y + DirectionToVector(LeftDirection()).y) ||
                BlockFreeOrNotConfident(
                    currentPos.x + 2 * DirectionToVector(currentDir).x,
                    currentPos.y + 2 * DirectionToVector(currentDir).y) ||
                BlockFreeOrNotConfident(
                    currentPos.x + 2 * DirectionToVector(currentDir).x + DirectionToVector(RightDirection()).x,
                    currentPos.y + 2 * DirectionToVector(currentDir).y + DirectionToVector(RightDirection()).y)) {
                return false;
            }
        } catch { } // it's a wall!
        return true;
    }

    static bool CanCalibrateByRightTurn() {
        try {
            if (
                //BlockFree(
                //    currentPos.x + 2 * DirectionToVector(RightDirection()).x + DirectionToVector(currentDir).x,
                //    currentPos.y + 2 * DirectionToVector(RightDirection()).y + DirectionToVector(currentDir).y) ||
                BlockFreeOrNotConfident(
                    currentPos.x + 2 * DirectionToVector(RightDirection()).x,
                    currentPos.y + 2 * DirectionToVector(RightDirection()).y) ||
                BlockFreeOrNotConfident(
                    currentPos.x + 2 * DirectionToVector(RightDirection()).x + DirectionToVector(OppositeDirection()).x,
                    currentPos.y + 2 * DirectionToVector(RightDirection()).y + DirectionToVector(OppositeDirection()).y)) {
                return false;
            }
        } catch { }
        return true;
    }

    public static void AvoidStartDeadLock() {
        if (_lastExploredCount <= 30) {
            for (int i = 0; i < 4; ++i) {
                for (int j = 0; j < 4; ++j) {
                    grids[i, j].gs = GridStatus.EMPTY;
                }
            }
        }
    }
    public static void TurnToDirection(Direction dir) {
        Instruction instru = Instruction.Stop;

        switch (DirectionToEuler(dir) - DirectionToEuler(currentDir)) {
            case -270:
            case 90:
                instru = Instruction.TurnRight;
                break;
            case -180:
            case 180:
                instru = Instruction.TurnBack;
                break;
            case -90:
            case 270:
                instru = Instruction.TurnLeft;
                break;
        }
        Debug.Log("Turning to " + dir + " direction by: " + instru);
        UpdateArduinoInstruction(instru);
        Act(instru);
    }

    static int CountExploredGrids() {
        int explored = 0;
        for (int i = 0; i < 15; i++) {
            for (int j = 0; j < 20; j++) {
                if (grids[i, j].gs != GridStatus.UNEXPLORED) {
                    explored++;
                }
            }
        }
        return explored;
    }

    static void CheckSwitchToFastestPath() {
        // check 8 grid loop
        if (currentPos.x == 1 && currentPos.y == 1 && _lastExploredCount > 295) {
            _usingRightHandWall = false;
        }

        if (lastPoses[totalSteps % 8].x == currentPos.x &&
            lastPoses[totalSteps % 8].y == currentPos.y) {
            if (lastDirs[totalSteps % 8] == currentDir) {
                _usingRightHandWall = false;
                return;
            }
        }
        lastPoses[totalSteps % 8] = currentPos;
        lastDirs[totalSteps % 8] = currentDir;

        // check larger loop
        _currExploredCount = CountExploredGrids();
        if (_currExploredCount - _lastExploredCount < 3) {
            countUsingRHWSteps++;
            if (countUsingRHWSteps >= 16) {
                _usingRightHandWall = false;
                countUsingRHWSteps = 0;
            }
        } else {
            _lastExploredCount = _currExploredCount;
            countUsingRHWSteps = 0;
        }
    }

    static bool FringeSafeToExplore(int i, int j) {
        try {
            if (grids[i, j].gs != GridStatus.EMPTY) {
                return false;
            }
        } catch { return false; }
        for (int a = -1; a < 2; a++) {
            for (int b = -1; b < 2; b++) {
                try {
                    if (grids[i + a, j + b].gs != GridStatus.EMPTY &&
                        grids[i + a, j + b].gs != GridStatus.VIRTUAL_WALL) {
                        return false;
                    }
                } catch { return false; }
            }
        }
        return true;
    }

    static Pos NextFringeToVisit() {
        for (int j = 19; j >= 0; j--) {
            for (int i = 14; i >= 0; i--) {
                if (grids[i, j].gs == GridStatus.UNEXPLORED) {
                    if (FringeSafeToExplore(i + 1, j - 2)) {
                        return new Pos(i + 1, j - 2);
                    } else if (FringeSafeToExplore(i + 2, j - 1)) {
                        return new Pos(i + 2, j - 1);
                    }
                }
            }
        }
        Debug.Log("Can't find path to unexplored grid! Exploration finished");
        return new Pos(-1, -1); // as null value
    }

    static string SimulateSingleSensorData(int checkX, int checkY, Direction dir, bool fromLeft) {
        int data = 1;
        for (int i = 0; i < 5; i++) {
            if (!fromLeft && i >= 3) { break; }
            try {
                if (_testBoard[checkX, checkY] == GridStatus.EMPTY) {
                    data += 1;
                } else {
                    break;
                }
            } catch { break; }
            checkX += DirectionToVector(dir).x;
            checkY += DirectionToVector(dir).y;
        }

        if (data > 3 && !(fromLeft && data <= 5)) {
            data = -1;
        } // -1 is empty, other possible values are 1,2,3

        return data.ToString();
    }

    static string SimulateSensorData() {
        Thread.Sleep((int)(1000 * timePerMove));
        string data = "";
        int checkX;
        int checkY;

        int curX = currentPos.x;
        int curY = currentPos.y;

        checkX = curX + 2 * DirectionToVector(currentDir).x + DirectionToVector(LeftDirection()).x;
        checkY = curY + 2 * DirectionToVector(currentDir).y + DirectionToVector(LeftDirection()).y;
        data += SimulateSingleSensorData(checkX, checkY, currentDir, false);
        data += ",";

        checkX = curX + 2 * DirectionToVector(currentDir).x;
        checkY = curY + 2 * DirectionToVector(currentDir).y;
        data += SimulateSingleSensorData(checkX, checkY, currentDir, false);
        data += ",";

        checkX = curX + 2 * DirectionToVector(currentDir).x + DirectionToVector(RightDirection()).x;
        checkY = curY + 2 * DirectionToVector(currentDir).y + DirectionToVector(RightDirection()).y;
        data += SimulateSingleSensorData(checkX, checkY, currentDir, false);
        data += ",";

        checkX = curX + DirectionToVector(currentDir).x + 2 * DirectionToVector(LeftDirection()).x;
        checkY = curY + DirectionToVector(currentDir).y + 2 * DirectionToVector(LeftDirection()).y;
        data += SimulateSingleSensorData(checkX, checkY, LeftDirection(), true);
        data += ",";

        checkX = curX + DirectionToVector(currentDir).x + 2 * DirectionToVector(RightDirection()).x;
        checkY = curY + DirectionToVector(currentDir).y + 2 * DirectionToVector(RightDirection()).y;
        data += SimulateSingleSensorData(checkX, checkY, RightDirection(), false);

        return data;
    }
    public static Direction LeftDirection() {
        switch (currentDir) {
            case Direction.NORTH:
                return Direction.WEST;
            case Direction.WEST:
                return Direction.SOUTH;
            case Direction.SOUTH:
                return Direction.EAST;
            case Direction.EAST:
                return Direction.NORTH;
            default:
                return currentDir;
        }
    }

    public static Direction RightDirection() {
        switch (currentDir) {
            case Direction.NORTH:
                return Direction.EAST;
            case Direction.WEST:
                return Direction.NORTH;
            case Direction.SOUTH:
                return Direction.WEST;
            case Direction.EAST:
                return Direction.SOUTH;
            default:
                return currentDir;
        }
    }

    public static Direction OppositeDirection() {
        switch (currentDir) {
            case Direction.NORTH:
                return Direction.SOUTH;
            case Direction.WEST:
                return Direction.EAST;
            case Direction.SOUTH:
                return Direction.NORTH;
            case Direction.EAST:
                return Direction.WEST;
            default:
                return currentDir;
        }
    }

    static bool BlockFree(int x, int y) {
        try {
            if (grids[x, y].gs == GridStatus.EMPTY || grids[x, y].gs == GridStatus.VIRTUAL_WALL) { return true; }
        } catch { }
        return false;
    }
    static bool BlockFreeOrNotConfident(int x, int y) {
        try {
            if (grids[x, y].confidence < 300 ||
                grids[x, y].gs == GridStatus.EMPTY || grids[x, y].gs == GridStatus.VIRTUAL_WALL) { return true; }
        } catch { } // it's a wooden wall!
        return false;
    }
    static bool BlockNotWall(int x, int y) {
        try {
            if (grids[x, y].gs == GridStatus.EMPTY || grids[x, y].gs == GridStatus.VIRTUAL_WALL || grids[x, y].gs == GridStatus.UNEXPLORED) { return true; }
        } catch { }
        return false;
    }

    static bool BlockUnexplored(int x, int y) {
        try {
            if (grids[x, y].gs == GridStatus.UNEXPLORED) { return true; }
        } catch { }
        return false;
    }

    // Check if grid is empty or unexplored
    static bool RowNotWall(Direction dir, int offset) {
        bool flag = false;

        int newX = currentPos.x + offset * DirectionToVector(dir).x;
        int newY = currentPos.y + offset * DirectionToVector(dir).y;

        if (BlockNotWall(newX, newY)) {
            switch (dir) {
                case Direction.NORTH:
                case Direction.SOUTH:
                    flag = BlockNotWall(newX + DirectionToVector(Direction.EAST).x, newY + DirectionToVector(Direction.EAST).y) &&
                        BlockNotWall(newX + DirectionToVector(Direction.WEST).x, newY + DirectionToVector(Direction.WEST).y);
                    break;
                case Direction.EAST:
                case Direction.WEST:
                    flag = BlockNotWall(newX + DirectionToVector(Direction.NORTH).x, newY + DirectionToVector(Direction.NORTH).y) &&
                        BlockNotWall(newX + DirectionToVector(Direction.SOUTH).x, newY + DirectionToVector(Direction.SOUTH).y);
                    break;
            }
        }
        return flag;
    }

    static bool RoomCheck(Direction dir) {
        int newX, newY, count, numOfOpenings;
        numOfOpenings = 0;
        count = 2;
        bool prevFlag = false;
        bool currFlag = true;
        bool isRoom = true;

        // Final value of count is the number of grids to check
        while (currFlag) {
            currFlag = RowNotWall(dir, count);
            count++;
        }
        count -= 1;

        // Check left side of 3x3; Not room if any grid is unexplored or if there're 3 grids in a row that the robot can go through
        for (int i = 0; i < count; i++) {
            newX = currentPos.x + i * DirectionToVector(dir).x;
            newY = currentPos.y + i * DirectionToVector(dir).y;

            switch (dir) {
                case Direction.NORTH:
                case Direction.SOUTH:
                    currFlag = BlockNotWall(newX + 2 * DirectionToVector(Direction.WEST).x, newY + 2 * DirectionToVector(Direction.WEST).y);
                    if (BlockUnexplored(newX + 2 * DirectionToVector(Direction.WEST).x, newY + 2 * DirectionToVector(Direction.WEST).y))
                        return false;
                    break;

                case Direction.EAST:
                case Direction.WEST:
                    currFlag = BlockNotWall(newX + 2 * DirectionToVector(Direction.SOUTH).x, newY + 2 * DirectionToVector(Direction.SOUTH).y);
                    if (BlockUnexplored(newX + 2 * DirectionToVector(Direction.SOUTH).x, newY + 2 * DirectionToVector(Direction.SOUTH).y))
                        return false;
                    break;
            }

            if (prevFlag && currFlag) {
                numOfOpenings += 1;
                if (numOfOpenings == 2)
                    isRoom = false;
            } else {
                numOfOpenings = 0;
            }
            prevFlag = currFlag;
        }

        numOfOpenings = 0;
        prevFlag = false;
        currFlag = false;

        // Check right side of 3x3; Not room if any grid is unexplored or if there're 3 grids in a row that the robot can go through
        // same to previous lines, just the direction inside currFlag is different
        for (int i = 0; i < count; i++) {
            newX = currentPos.x + i * DirectionToVector(dir).x;
            newY = currentPos.y + i * DirectionToVector(dir).y;

            switch (dir) {
                case Direction.NORTH:
                case Direction.SOUTH:
                    currFlag = BlockNotWall(newX + 2 * DirectionToVector(Direction.EAST).x, newY + 2 * DirectionToVector(Direction.EAST).y);
                    if (BlockUnexplored(newX + 2 * DirectionToVector(Direction.EAST).x, newY + 2 * DirectionToVector(Direction.EAST).y))
                        return false;
                    break;
                case Direction.EAST:
                case Direction.WEST:
                    currFlag = BlockNotWall(newX + 2 * DirectionToVector(Direction.NORTH).x, newY + 2 * DirectionToVector(Direction.NORTH).y);
                    if (BlockUnexplored(newX + 2 * DirectionToVector(Direction.NORTH).x, newY + 2 * DirectionToVector(Direction.NORTH).y))
                        return false;
                    break;
            }

            if (prevFlag && currFlag) {
                numOfOpenings += 1;
                if (numOfOpenings == 2)
                    isRoom = false;
            } else {
                numOfOpenings = 0;
            }
            prevFlag = currFlag;
        }
        return isRoom;
    }

    static bool ExploredLimitFulfilled(int limit) {
        int explored = 0;

        for (int i = 0; i < 15; i++) {
            for (int j = 0; j < 20; j++) {
                if (grids[i, j].gs != GridStatus.UNEXPLORED) {
                    explored++;
                }
            }
        }

        return explored >= limit;
    }

    public static void CheckSendTakePicture(Instruction instru) {
        if (!toggleImageFlag) { return; }
        string msg = "";
        if (instru == Instruction.Forward) {
            if (ShouldTakePicture(
                    currentPos.x + 2 * DirectionToVector(RightDirection()).x + DirectionToVector(OppositeDirection()).x,
                    currentPos.y + 2 * DirectionToVector(RightDirection()).y + DirectionToVector(OppositeDirection()).y,
                    LeftDirection())) {
                GetTakePicturePosStrings(ref msg);
                SendPictureCoordinates(msg);
            }
        } else if (instru == Instruction.TurnLeft || instru == Instruction.TurnBack) {
            GetTakePicturePosStrings(ref msg);
            if (msg != "nullnullnull") {
                SendPictureCoordinates(msg);
            }
        }
    }

    public static void GetTakePicturePosStrings(ref string msg) {
        if (ShouldTakePicture(
                currentPos.x + 2 * DirectionToVector(RightDirection()).x + DirectionToVector(currentDir).x,
                currentPos.y + 2 * DirectionToVector(RightDirection()).y + DirectionToVector(currentDir).y,
                LeftDirection())) {
            msg += GetPosXYToMsgFormatAndMarkVisited(
                currentPos.x + 2 * DirectionToVector(RightDirection()).x + DirectionToVector(currentDir).x,
                currentPos.y + 2 * DirectionToVector(RightDirection()).y + DirectionToVector(currentDir).y,
                LeftDirection());
        } else {
            msg += "null";
        }
        if (ShouldTakePicture(
                currentPos.x + 2 * DirectionToVector(RightDirection()).x,
                currentPos.y + 2 * DirectionToVector(RightDirection()).y,
                LeftDirection())) {
            msg += GetPosXYToMsgFormatAndMarkVisited(
                currentPos.x + 2 * DirectionToVector(RightDirection()).x,
                currentPos.y + 2 * DirectionToVector(RightDirection()).y,
                LeftDirection());
        } else {
            msg += "null";
        }
        if (ShouldTakePicture(
                currentPos.x + 2 * DirectionToVector(RightDirection()).x + DirectionToVector(OppositeDirection()).x,
                currentPos.y + 2 * DirectionToVector(RightDirection()).y + DirectionToVector(OppositeDirection()).y,
                LeftDirection())) {
            msg += GetPosXYToMsgFormatAndMarkVisited(
                currentPos.x + 2 * DirectionToVector(RightDirection()).x + DirectionToVector(OppositeDirection()).x,
                currentPos.y + 2 * DirectionToVector(RightDirection()).y + DirectionToVector(OppositeDirection()).y,
                LeftDirection());
        } else {
            msg += "null";
        }
    }
    public static bool ShouldTakePicture(int x, int y, Direction picDir) {
        string s = PosXYToMsgFormat(x, y) + picDir.ToString();
        //Debug.Log(s);
        try {
            if (grids[x, y].gs == GridStatus.WALL && !visitedEdges.Contains(s)) {
                return true;
            }
        } catch { }
        return false;
    }

    private static string GetPosXYToMsgFormatAndMarkVisited(int x, int y, Direction dir) {
        string s = PosXYToMsgFormat(x, y) + dir.ToString();
        visitedEdges.Add(s);
        return PosXYToMsgFormat(x, y);
    }

    public static string PosXYToMsgFormat(int x, int y) {
        string msg = "";
        if (x <= 9) {
            msg += "0";
        }
        msg += x.ToString();

        if (y <= 9) {
            msg += "0";
        }
        msg += y.ToString();

        return msg;
    }

    private static void UpdateNotYetTaken() {
        if (!toggleImageFlag) { return; }
        for (int i = 0; i < 15; i++) {
            for (int j = 0; j < 20; j++) {
                if (grids[i, j].gs == GridStatus.WALL) {
                    foreach (Direction dir in directionsForExplore) {
                        if (CheckNeighbourGrid(i + DirectionToVector(dir).x, j + DirectionToVector(dir).y)) {
                            notYetTakenSurfaces.Add(PosXYToMsgFormat(i, j) + dir.ToString());
                        }
                    }
                }
            }
        }

        string lastStr = "";
        int lastShortest = -1;
        int x = -1;
        string lastDir = "";
        int y = -1;
        string direction = "";
        string lastSur = "";
        bool hasUnvisited = false;
        do {
            x = -1;
            y = -1;
            lastStr = "";
            lastDir = "";
            lastShortest = -1;
            hasUnvisited = false;
            foreach (var sur in notYetTakenSurfaces) {

                //if (photosTaken == 5) break; 

                if (!visitedEdges.Contains(sur)) {
                    hasUnvisited = true;
                    string surface = CheckValidSurface(sur);
                    if (surface != "invalid") {
                      

                        x = ParseStringToInteger(surface.Substring(0, 2));
                        y = ParseStringToInteger(surface.Substring(2, 2));
                        direction = surface.Substring(4); // direction
                        if (lastStr == "" && lastShortest == -1) {
                            lastSur = sur;
                            lastDir = direction;
                            lastStr = surface;
                            lastShortest = FastestPath.AStarSearch(new Pos(1,1), new Pos(x, y), grids, false).Count;
                        } else if (FastestPath.AStarSearch(new Pos(1, 1), new Pos(x, y), grids, false).Count > lastShortest) {
                            lastSur = sur;
                            lastStr = surface;
                            lastDir = direction;
                            lastShortest = FastestPath.AStarSearch(new Pos(1, 1), new Pos(x, y), grids, false).Count;
                        }

                    } else {
                        visitedEdges.Add(sur);
                        Debug.Log("No Valid location for surface: " + sur);
                    }
                }
            }

            if (hasUnvisited) {
                 Debug.Log ("Going to x: " +
                     lastStr.Substring (0, 2) +
                     " ,y: " + lastStr.Substring (2, 2) +
                     " Facing: " + lastStr.Substring (4) +
                     " to capture surface: " + lastSur);
                MoveRobotToTarget(new Pos(ParseStringToInteger(lastStr.Substring(0, 2)), ParseStringToInteger(lastStr.Substring(2, 2))));
                visitedEdges.Add(lastSur);
                // Change direction if needed
                foreach (Direction dir in directionsForExplore) {
                    if (dir.ToString() == lastStr.Substring(4)) {
                        if (currentDir != dir) {
                            TurnToDirection(dir);
                        }
                    }
                }
            }

        } while (hasUnvisited);

        MoveRobotToTarget(new Pos(1, 1));
    }

    private static bool CheckNeighbourGrid(int x, int y) {
        try {
            return (grids[x, y].gs != GridStatus.WALL && x >= 0 && x < 15 && y >= 0 && y < 20);

        } catch { return false; }

    }

    private static int ParseStringToInteger(string intString) {

        int i = 0;
        if (!Int32.TryParse(intString, out i)) {
            i = -1;
        }
        return i;
    }

    private static bool Valid3x3(int x, int y) {
        try {
            if (!BlockFree(x, y)) {
                return false;
            } else {
                for (int a = -1; a < 2; a++) {
                    for (int b = -1; b < 2; b++) {

                        if (!BlockFree(x + a, y + b)) {
                            return false;

                        }
                    }
                }
            }
        } catch { return false; }
        return true;
    }
    private static string CheckValidSurface(string surface) {

        int x = ParseStringToInteger(surface.Substring(0, 2));
        int y = ParseStringToInteger(surface.Substring(2, 2));
        string direction = surface.Substring(4);
        for (int a = -1; a < 2; a++) {
            switch (direction) {
                case "NORTH":
                    if (Valid3x3(x + a, y + 2))
                        return PosXYToMsgFormat(x + a, y + 2) + Direction.EAST.ToString();
                    break;
                case "SOUTH":
                    if (Valid3x3(x + a, y - 2))
                        return PosXYToMsgFormat(x + a, y - 2) + Direction.WEST.ToString();
                    break;
                case "WEST":
                    if (Valid3x3(x - 2, y + a))
                        return PosXYToMsgFormat(x - 2, y + a) + Direction.NORTH.ToString();
                    break;
                case "EAST":
                    if (Valid3x3(x + 2, y + a))
                        return PosXYToMsgFormat(x + 2, y + a) + Direction.SOUTH.ToString();
                    break;

            }
        }

        return "invalid";
    }

    //static bool HasWall(Direction dir, Pos currPos) {
    //    int newX = currPos.x + 2 * DirectionToVector(dir).x;
    //    int newY = currPos.y + 2 * DirectionToVector(dir).y;
    //    if (BlockFree(newX, newY)) {
    //        switch (dir) {
    //            case Direction.NORTH:
    //            case Direction.SOUTH:
    //                if ((!BlockFree(newX + DirectionToVector(Direction.EAST).x, newY + DirectionToVector(Direction.EAST).y))
    //                    || (!BlockFree(newX + DirectionToVector(Direction.WEST).x, newY + DirectionToVector(Direction.WEST).y))) {
    //                    return true;
    //                }
    //                break;
    //            case Direction.EAST:
    //            case Direction.WEST:
    //                if ((!BlockFree(newX + DirectionToVector(Direction.NORTH).x, newY + DirectionToVector(Direction.NORTH).y))
    //                    || (!BlockFree(newX + DirectionToVector(Direction.SOUTH).x, newY + DirectionToVector(Direction.SOUTH).y))) {
    //                    return true;
    //                }
    //                break;
    //        }
    //    } else { return true; }
    //    return false;
    //}

    //static void MoveToNearestWall() {
    //    Direction closestDir;
    //    int leastSteps = 50;

    //    foreach (Direction dir in directionsForExplore) {

    //        Pos tempPos = currentPos;

    //        for (int i = 0; i < 20; i++) {
    //            // Loops till we find a position next to an obstacle or wall
    //            if (HasWall(dir, tempPos)) {
    //                break;
    //            }

    //            tempPos = new Pos(currentPos.x + DirectionToVector(dir).x, currentPos.y + DirectionToVector(dir).y);

    //            if (i < leastSteps) {
    //                leastSteps = i;
    //                closestDir = dir;
    //            }
    //        }

    //        for (int i = 0; i < leastSteps; i++) {
    //            MoveByNextDirection(currentDir, dir);
    //        }
    //    }
    //}

    //static void AdjustForRightHandWall() {
    //    if (!HasWall(RightDirection(), currentPos)) {
    //        if (HasWall(currentDir, currentPos)) {
    //            //Turn Left
    //            UpdateSendMessages(Instruction.TurnLeft);
    //            Act(Instruction.TurnLeft);
    //            UpdateBoard(GetSensorBuffer());
    //        } else if (HasWall(OppositeDirection(), currentPos)) {
    //            //Turn Right
    //            UpdateSendMessages(Instruction.TurnRight);
    //            Act(Instruction.TurnRight);
    //            UpdateBoard(GetSensorBuffer());
    //        } else if (HasWall(LeftDirection(), currentPos)) {
    //            //Turn Around
    //            UpdateSendMessages(Instruction.TurnRight);
    //            Act(Instruction.TurnRight);
    //            UpdateBoard(GetSensorBuffer());
    //            UpdateSendMessages(Instruction.TurnRight);
    //            Act(Instruction.TurnRight);
    //            UpdateBoard(GetSensorBuffer());
    //        } else {
    //            MoveToNearestWall();
    //            AdjustForRightHandWall(); //Adjust again because after moving will be facing wall
    //        }
    //    } // If already have right wall don't need to move
    //}

    #endregion

    public static void Init(Pos currentPos, Direction currentDir) {
        for (int i = 0; i < 15; i++) {
            for (int j = 0; j < 20; j++) {
                grids[i, j].UpdateStatus(GridStatus.UNEXPLORED);
            }
        }
        for (int i = -1; i < 2; i++) {
            for (int j = -1; j < 2; j++) {
                int x = currentPos.x;
                int y = currentPos.y;
                grids[x + i, y + j].UpdateStatus(GridStatus.EMPTY);
                grids[1 + i, 1 + j].confidence = -9000;
                grids[i + 13, j + 18].confidence = -9000;
            }
        }
        for (int i = -1; i < 2; i++) {
            switch (currentDir) {
                case Direction.NORTH:
                case Direction.SOUTH:
                    grids[currentPos.x + i, currentPos.y + 2 * DirectionToVector(currentDir).y].UpdateStatus(GridStatus.EMPTY);
                    break;
                case Direction.EAST:
                case Direction.WEST:
                    grids[currentPos.x + 2 * DirectionToVector(currentDir).x, currentPos.y + i].UpdateStatus(GridStatus.EMPTY);
                    break;
            }
        }
        totalSteps = 0;
        for (int i = 0; i < 8; i++) {
            lastPoses[i] = new Pos(-1, -1);
            lastDirs[i] = currentDir;
        }
        totalSteps = 0;
        _lastExploredCount = 0;
        Algo.currentPos = currentPos;
        Algo.currentDir = currentDir;
        _testing = false;
        _hasNewSensorData = false;
    }

    public static void Init(Pos currentPos, Direction currentDir, GridStatus[,] testBoard) {
        for (int i = 0; i < 15; i++) {
            for (int j = 0; j < 20; j++) {
                grids[i, j].UpdateStatus(GridStatus.UNEXPLORED);
            }
        }
        for (int i = 0; i < 3; i++) {
            for (int j = 0; j < 3; j++) {
                grids[i, j].UpdateStatus(GridStatus.EMPTY);
                grids[i + 12, j + 17].confidence = -9000;
            }
        }
        for (int i = -1; i < 2; i++) {
            switch (currentDir) {
                case Direction.NORTH:
                case Direction.SOUTH:
                    grids[currentPos.x + i, currentPos.y + 2 * DirectionToVector(currentDir).y].UpdateStatus(GridStatus.EMPTY);
                    break;
                case Direction.EAST:
                case Direction.WEST:
                    grids[currentPos.x + 2 * DirectionToVector(currentDir).x, currentPos.y + i].UpdateStatus(GridStatus.EMPTY);
                    break;
            }
        }
        totalSteps = 0;
        _lastExploredCount = 0;
        Algo.currentPos = currentPos;
        Algo.currentDir = currentDir;
        _testing = true;
        _testBoard = testBoard;
    }

    private static string GetSensorBuffer() {
        if (_testing) {
            return SimulateSensorData();
        }

        string result;
        while (true) {
            _bufferMutex.WaitOne();
            if (!_hasNewSensorData) {
                _bufferMutex.ReleaseMutex();
                continue;
            }
            result = _sensorBuffer;
            _hasNewSensorData = false;
            _bufferMutex.ReleaseMutex();
            break;
        }
        return result;
    }

    public static void SetSensorBuffer(string incomingMsg) {
        _bufferMutex.WaitOne();
        _hasNewSensorData = true;
        _sensorBuffer = incomingMsg;
        _bufferMutex.ReleaseMutex();
    }

    public static void CheckCalibrate() {
        if (CanCalibratedUsingFrontWall()) {
            if (CanCalibrateByRightTurn()) {
                calibrateInstru = Instruction.CalibrateRightCorner;
            } else {
                calibrateInstru = Instruction.CalibrateFront; // front calibration
            }

            rightWallCounter = STEPS_EVERY_CALIBRATE;
            overallCalibrateCounter = STEPS_EVERY_CALIBRATE * 2;
        } else if (CanCalibrateByRightTurn()) {
            if (rightWallCounter < 1 || overallCalibrateCounter < 1) {
                calibrateInstru = Instruction.CalibrateRightWall; // right wall calibration
                rightWallCounter = STEPS_EVERY_CALIBRATE;
                overallCalibrateCounter = STEPS_EVERY_CALIBRATE * 2;
            }
            rightWallCounter -= 1;
        }
        overallCalibrateCounter -= 1;
        Debug.Log("Wall counters are: " + rightWallCounter + ", " + overallCalibrateCounter);
    }

    //==========================
    // Sensor Formation
    // 
    //   abc
    //  dXXXe
    //   XXX
    //   XXX
    //
    // String : "a, b, c, d, e"
    //==========================
    static void UpdateBoard(string rawData) {
        int[] data = Array.ConvertAll(rawData.Split(','), int.Parse);
        int updateX = currentPos.x + DirectionToVector(currentDir).x;
        int updateY = currentPos.y + DirectionToVector(currentDir).y;

        UpdateBoardSingleLine(data[3], LeftDirection(),
            updateX + 2 * DirectionToVector(LeftDirection()).x,
            updateY + 2 * DirectionToVector(LeftDirection()).y, true);
        UpdateBoardSingleLine(data[4], RightDirection(),
            updateX + 2 * DirectionToVector(RightDirection()).x,
            updateY + 2 * DirectionToVector(RightDirection()).y, false);

        updateX = updateX + DirectionToVector(currentDir).x + DirectionToVector(LeftDirection()).x;
        updateY = updateY + DirectionToVector(currentDir).y + DirectionToVector(LeftDirection()).y;
        for (int i = 0; i < 3; i++) {
            UpdateBoardSingleLine(data[i], currentDir, updateX, updateY, false);
            updateX += DirectionToVector(RightDirection()).x;
            updateY += DirectionToVector(RightDirection()).y;
        }
    }

    static void UpdateBoardSingleLine(int data, Direction dir, int updateX, int updateY, bool fromLeft) {
        if (data == -1) {
            data = 6;
        }
        for (int i = 1; i <= 5; i++) {
            UpdateGridStatus(updateX, updateY, i, i == data, fromLeft);
            if (i == data || (!fromLeft && i >= 3)) {
                break; // shouldn't update those behind a known block
            }
            updateX += DirectionToVector(dir).x;
            updateY += DirectionToVector(dir).y;
        }
    }

    private static void UpdateGridStatus(int updateX, int updateY, int i, bool hasWall, bool fromLeft) {
        int delta;
        switch (i) {
            case 1:
                delta = 300;
                break;
            case 2:
                delta = 100;
                break;
            case 3:
                delta = 10;
                break;
            default:
                delta = 1;
                break;
        }
        //(int)Math.Pow(10, 3 - i); // 1: 100, 2: 10, 3: 1
        if (!hasWall) {
            delta = -delta;
        }
        try { grids[updateX, updateY].UpdateStatus(delta); } catch { }
    }

    static bool CheckDirectionCanMove(Direction dir) {
        // if (RoomCheck (dir)) {
        //     return false;
        // }

        bool canMove = false;
        int newX = currentPos.x + 2 * DirectionToVector(dir).x;
        int newY = currentPos.y + 2 * DirectionToVector(dir).y;
        if (BlockFree(newX, newY)) {
            switch (dir) {
                case Direction.NORTH:
                case Direction.SOUTH:
                    canMove = BlockFree(newX + DirectionToVector(Direction.EAST).x, newY + DirectionToVector(Direction.EAST).y) &&
                        BlockFree(newX + DirectionToVector(Direction.WEST).x, newY + DirectionToVector(Direction.WEST).y);
                    break;
                case Direction.EAST:
                case Direction.WEST:
                    canMove = BlockFree(newX + DirectionToVector(Direction.NORTH).x, newY + DirectionToVector(Direction.NORTH).y) &&
                        BlockFree(newX + DirectionToVector(Direction.SOUTH).x, newY + DirectionToVector(Direction.SOUTH).y);
                    break;
            }
        }
        return canMove;
    }

    static Direction DecideNextMove() {
        Direction nextMove;

        if (CheckDirectionCanMove(RightDirection())) {
            nextMove = RightDirection();
        } else if (CheckDirectionCanMove(currentDir)) {
            nextMove = currentDir;
        } else if (CheckDirectionCanMove(LeftDirection())) {
            nextMove = LeftDirection();
        } else {
            nextMove = OppositeDirection();
        }
        return nextMove;
    }

    static void MoveRobotToTarget(Pos targetPos) {
        CreateVirtualWall(false);
        List<Direction> dirs = FastestPath.AStarSearch(currentPos, targetPos, grids, false);
        if (null != dirs) {
            foreach (Direction nextMove in dirs) {
                // Debug.Log ("TargetPos = " + targetPos.ToString () + ", " +
                //     "currentPos = " + currentPos.ToString () + ", " +
                //     "currentDir = " + currentDir + ", " +
                //     "next move is: " + nextMove);
                MoveByNextDirection(currentDir, nextMove);

                //_currExploredCount = CountExploredGrids();
                //if (_currExploredCount - _lastExploredCount >= 3) {
                //    AdjustForRightHandWall();
                //    Debug.Log("Exit FP when having new explored, robot at: " + currentPos.x + ", " + currentPos.y);
                //    return;
                //} 
                //_lastExploredCount = _currExploredCount;
            }
        }
        Debug.Log("Exit FP, robot at: " + currentPos.x + ", " + currentPos.y);
    }

    static void MoveByNextDirection(Direction currentDir, Direction nextMove) {
        foreach (Instruction instru in GetInstructions(currentDir, nextMove, false)) {
            CheckSendTakePicture(instru);
            UpdateAndroidMap();
            UpdateArduinoInstruction(instru);
            Act(instru);
            UpdateBoard(GetSensorBuffer());
            CheckCalibrate();
        }
    }

    static void StopExploration(object source, ElapsedEventArgs e) {
        _underSixMinutes = false;
        //UpdateArduinoInstruction(Instruction.Stop);
        //Algo.Act(Instruction.Stop);
    }

    public static void ExplorationAlgoTimeLimit() {

        int limit = (int)(timeLimit / timePerMove);
        int steps = 0;

        while (Contains2D(grids, GridStatus.UNEXPLORED) && (steps < limit)) {
            string rawData = SimulateSensorData();
            UpdateBoard(rawData);
            Direction nextMove = DecideNextMove();
            foreach (Instruction instru in GetInstructions(currentDir, nextMove, false)) {
                steps++;
                Act(instru);
                Thread.Sleep((int)(1000 * timePerMove));
            }
        }

        inExploration = false;
    }

    public static void ExplorationAlgoCoverage() {
        // coverageLimit is in % of explored squares
        int limit = 3 * coveragePercentage; // 300*(coverageLimit/100) = 3 * coverageLimit

        while (Contains2D(grids, GridStatus.UNEXPLORED)) {
            string rawData = SimulateSensorData();
            UpdateBoard(rawData);
            if (ExploredLimitFulfilled(limit)) {
                break;
            }
            Direction nextMove = DecideNextMove();
            foreach (Instruction instru in GetInstructions(currentDir, nextMove, false)) {
                Act(instru);
                Thread.Sleep((int)(1000 * timePerMove));
            }
        }

        inExploration = false;
    }

    public static void ExplorationAlgo() {

        // Set Timer for 5 minutes and 45 seconds
        endTimer = new System.Timers.Timer(345000);
        endTimer.Elapsed += StopExploration;
        endTimer.Enabled = true;

        Pos nextPos;
        Direction nextMove = currentDir;

        while (!ExploredLimitFulfilled(300) && _underSixMinutes) {
            // as if we call DecideNextMove() first
            if (_usingRightHandWall) {
                MoveByNextDirection(currentDir, nextMove);
                CheckSwitchToFastestPath();
            } else {
                nextPos = NextFringeToVisit();
                Debug.Log("Inside Fastest. Target: " + nextPos.x + ", " + nextPos.y);
                if (nextPos.x == -1) { // if null
                    break;
                }
                MoveRobotToTarget(nextPos);
                _usingRightHandWall = true;
                _lastExploredCount = CountExploredGrids();
                AvoidStartDeadLock();
            }
            nextMove = DecideNextMove();
        }

        if (_underSixMinutes) {
            MoveRobotToTarget(new Pos(1, 1)); // move back to start
            // UpdateArduinoInstruction(Instruction.CalibrateStart); // robot will definitely calibrate at start point by existing wall check
        }

        FlipLongSensorRangeDetection();
        UpdateAndroidMap();
        SendMDF();
        //UpdateNotYetTaken();
        TurnToDirection(Direction.EAST); // hardcoded facing east after start calibration
        CreateVirtualWall(true); // this is after sending MDF!
        hasCompleteMap = true;

        inExploration = false;
        Debug.Log("Exploration finished! Robot at: " + currentPos.x + ", " + currentPos.y +
            ". Total Steps = " + totalSteps);
    }

}