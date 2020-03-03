using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using static Arena;
using static Exploration;
using static FastestPath;

// Main static class of Algorithm
public class Algo : MonoBehaviour {

    // major map elements
    public static Grid[,] grids = new Grid[15, 20];
    public static GridStatus[,] gridStatuses = new GridStatus[15, 20];
    public static Pos currentPos;
    public static Direction currentDir; // shouldn't equal Direction.STOP anytime!

    public static bool hasCompleteMap = false;
    public static bool inExploration = false;
    public static bool inFastestPath = false;

    // components
    public static SocketClient client = new SocketClient();
    public static bool connected = false;
    public static readonly string DEFAULT_MAP_PATH = "Assets/20_15_test.txt";
    public static Thread explorationThread;
    public static int initiateInstruction = -1;
    public static Mutex androidInitiateMutex = new Mutex();

    // settings from Simulation app
    public static float timePerMove = 0.5f;
    public static bool diagonal = false;
    public static bool toggleImageFlag = true;
    public static float timeLimit;
    public static bool hasTimeLimit = false;
    public static int coveragePercentage = 100;

    // settings from Android
    public static Pos startingPos = new Pos(1, 1);
    public static Direction startingDir = Direction.EAST;
    public static Pos waypoint = new Pos(-1, -1); // NULL
    //public static Pos startingPos = new Pos(6, 1);
    //public static Direction startingDir = Direction.NORTH;
    //public static Pos waypoint = new Pos(5, 15);


    // ---------------------------------------------- //
    // ------------ Instruction Component ----------- //
    // ---------------------------------------------- //
    public static void Act(Instruction instru) {
        totalSteps++;
        switch (instru) {
            case Instruction.Forward:
            case Instruction.ForwardD:
                currentPos = new Pos(
                    currentPos.x + DirectionToVector(currentDir).x,
                    currentPos.y + DirectionToVector(currentDir).y);
                break;
            case Instruction.TurnLeft:
                if (!CanCalibratedUsingFrontWall()) { overallCalibrateCounter = 0; }
                currentDir = EulerToDirection((DirectionToEuler(currentDir) - 90) % 360);
                break;
            case Instruction.TurnRight:
                if (!CanCalibratedUsingFrontWall()) { overallCalibrateCounter = 0; }
                currentDir = EulerToDirection((DirectionToEuler(currentDir) + 90) % 360);
                break;
            case Instruction.TurnLeftD:
                currentDir = EulerToDirection((DirectionToEuler(currentDir) - 45) % 360);
                break;
            case Instruction.TurnRightD:
                currentDir = EulerToDirection((DirectionToEuler(currentDir) + 45) % 360);
                break;
            case Instruction.TurnBack:
                if (!CanCalibratedUsingFrontWall()) { overallCalibrateCounter = 0; }
                currentDir = EulerToDirection((DirectionToEuler(currentDir) + 180) % 360);
                break;
            case Instruction.Backward:
                currentPos = new Pos(
                    currentPos.x - DirectionToVector(currentDir).x,
                    currentPos.y - DirectionToVector(currentDir).y);
                break;
            default: break;
        }
    }

    public static List<Instruction> ConvertToInstructionList(List<Direction> solution, bool diagonal) {
        List<Instruction> result = new List<Instruction>();

        if (diagonal) {
            solution = ConvertDirectionsToDiagonal(solution);
        }
        Direction prevDir = currentDir;
        while (solution.Count > 0) {
            Direction currDir = solution[0];
            foreach (Instruction i in GetInstructions(prevDir, currDir, diagonal)) {
                result.Add(i);
                Debug.Log(i);
            }
            solution.RemoveAt(0);
            prevDir = currDir;
        }
        return result;
    }

    public static List<Direction> ConvertDirectionsToDiagonal(List<Direction> solution) {
        List<Direction> result = new List<Direction> { solution[0] };
        bool popped = false;
        for (int i = 1; i < solution.Count; i++) {
            Pos p1 = DirectionToVector(solution[i]);
            Pos p2 = DirectionToVector(solution[i - 1]);
            Pos comb = new Pos(p1.x + p2.x, p1.y + p2.y);
            float dist = comb.x * comb.x + comb.y + comb.y;
            if (!popped && 1 < dist && dist < 4) {
                result.RemoveAt(result.Count - 1);
                result.Add(VectorToDirection(comb));
                popped = true; // avoid consecutive popping of diagonal directions
            } else {
                result.Add(solution[i]);
                popped = false;
            }
        }
        // foreach (Direction dir in result) { Debug.Log("Conversion: " + dir); }
        return result;
    }
    public static List<Instruction> GetInstructions(Direction prevDir, Direction currDir, bool diagonal) {
        List<Instruction> result = new List<Instruction>();

        Instruction instru = Instruction.Stop;
        switch ((DirectionToEuler(currDir) - DirectionToEuler(prevDir))) {
            case -315:
            case 45: if (diagonal) { instru = Instruction.TurnRightD; } break;
            case -270:
            case 90: instru = Instruction.TurnRight; break;
            case -180:
            case 180:
                result.Add(Instruction.TurnLeft);
                instru = Instruction.TurnLeft; break;
            case -90:
            case 270: instru = Instruction.TurnLeft; break;
            case -45:
            case 315: if (diagonal) { instru = Instruction.TurnLeftD; } break;
        }
        if (instru != Instruction.Stop) {
            result.Add(instru);
        }

        // Move amount only depends on current move!
        if (diagonal &&
            (DirectionToEuler(currDir) - DirectionToEuler(Direction.NORTH)) % 90 == 45) {
            result.Add(Instruction.ForwardD);
        } else {
            result.Add(Instruction.Forward);
        }
        return result;
    }


    // ---------------------------------------------- //
    // ------------   Message Component   ----------- //
    // ---------------------------------------------- //

    public static int GetInitiateInstruction() {
        androidInitiateMutex.WaitOne();
        int i = initiateInstruction;
        androidInitiateMutex.ReleaseMutex();
        return i;
    }
    public static void SetInitiateInstruction(int i) {
        androidInitiateMutex.WaitOne();
        initiateInstruction = i;
        androidInitiateMutex.ReleaseMutex();
    }


    public static string BinaryToHex(string b, int length) {
        string hex = "";
        string temp;
        int x = 0;

        while (x < length) {
            temp = b.Substring(x, 4);
            hex += Convert.ToInt32(temp, 2).ToString("X");
            x += 4;
        }

        return hex;
    }

    public static string[] GenerateMapDescriptor(Grid[,] board) {
        string exploredB = "11";
        string obstacleB = "";
        int exploredNum = 0;

        for (int j = 0; j < 20; j++) {
            for (int i = 0; i < 15; i++) {
                if (board[i, j].gs == GridStatus.UNEXPLORED) {
                    exploredB += "0";
                } else {
                    exploredNum++;
                    exploredB += "1";
                    if (board[i, j].gs == GridStatus.EMPTY || board[i, j].gs == GridStatus.VIRTUAL_WALL) {
                        obstacleB += "0";
                    } else if (board[i, j].gs == GridStatus.WALL) {
                        obstacleB += "1";
                    } else {
                        // not adding unexplored grids
                    }
                }
            }
        }

        exploredB += "11";
        int paddingNum = 8 - (exploredNum % 8);

        // TODO: is there a more efficient way of doing this?
        for (int i = 0; i < paddingNum; i++) {
            obstacleB += "0";
        }

        return (new string[2] { BinaryToHex(exploredB, 304), BinaryToHex(obstacleB, (exploredNum + paddingNum)) });
    }

    public static string AndroidProtocolMessage() {

        string msg = "";

        if (currentPos.x <= 9) {
            msg += "0" + currentPos.x.ToString();
        } else {
            msg += currentPos.x.ToString();
        }
        if (currentPos.y <= 9) {
            msg += "0" + currentPos.y.ToString();
        } else {
            msg += currentPos.y.ToString();
        }

        switch (currentDir) {
            case Direction.NORTH: msg += "0"; break;
            case Direction.SOUTH: msg += "1"; break;
            case Direction.EAST: msg += "2"; break;
            case Direction.WEST: msg += "3"; break;
            case Direction.NORTHEAST: msg += "4"; break;
            case Direction.SOUTHEAST: msg += "5"; break;
            case Direction.NORTHWEST: msg += "6"; break;
            case Direction.SOUTHWEST: msg += "7"; break;
            default: msg += "0"; break;
        }

        if (inExploration) {
            string mapB = "";
            for (int j = 0; j < 20; j++) {
                for (int i = 0; i < 15; i++) {
                    switch (grids[i, j].gs) {
                        case (GridStatus.UNEXPLORED):
                            mapB += "00";
                            break;
                        case (GridStatus.IMAGE):
                        case (GridStatus.WALL):
                            mapB += "01";
                            break;
                        case (GridStatus.EMPTY):
                            mapB += "10";
                            break;
                        case (GridStatus.VIRTUAL_WALL):
                            mapB += "11";
                            break;
                        default:
                            mapB += "00"; break;
                    }
                }
            }
            msg += BinaryToHex(mapB, 600);
        }
        return msg;
    }


    public static void UpdateAndroidMap() {
        client.SendMessage("B4", AndroidProtocolMessage());
    }
    public static void SendMDF() {
        string[] mdf = GenerateMapDescriptor(grids);
        Debug.Log("MDF 1:" + mdf[0]);
        Debug.Log("MDF 2:" + mdf[1]);
        client.SendMessage("B5", "MDF 1:" + mdf[0] + ", MDF 2:" + mdf[1]);
    }
    public static void UpdateArduinoInstruction(Instruction instru) {
        string msg;
        if (Exploration.calibrateInstru != Instruction.Stop) {
            if (Exploration.calibrateInstru == Instruction.CalibrateRightCorner) {
                msg = InstructionToChar(Instruction.CalibrateRightWall).ToString()
                                   + InstructionToChar(Instruction.CalibrateFront).ToString()
                                   + InstructionToChar(instru).ToString()
                                   + InstructionToChar(Instruction.ReadSensor).ToString();
            } else {
                msg = InstructionToChar(Exploration.calibrateInstru).ToString()
                                   + InstructionToChar(instru).ToString()
                                   + InstructionToChar(Instruction.ReadSensor).ToString();
            }
            Exploration.calibrateInstru = Instruction.Stop;
        } else {
            msg = InstructionToChar(instru).ToString() + InstructionToChar(Instruction.ReadSensor).ToString();
        }
        client.SendMessage("A1", msg);
    }
    public static void SendFastestPathInstructions(List<Instruction> instrus) {
        string msg = "";
        int countForward = 0;
        foreach (Instruction instru in instrus) {
            if (instru == Instruction.Forward) {
                countForward++;
            } else {
                FastestPathConvertForwardNumberString(ref countForward, ref msg);
                if (instru == Instruction.TurnLeft) {
                    msg += InstructionToChar(Instruction.TurnLeftF).ToString();
                } else if (instru == Instruction.TurnRight) {
                    msg += InstructionToChar(Instruction.TurnRightF).ToString();
                } else {
                    msg += InstructionToChar(instru).ToString();
                }
            }
        }
        FastestPathConvertForwardNumberString(ref countForward, ref msg);
        client.SendMessage("A1", msg);
    }
    static void FastestPathConvertForwardNumberString(ref int countForward, ref string msg) {
        if (countForward > 0) {
            while (countForward > FP_MAX_STEPS) { // won't exceed 18
                msg += FP_MAX_STEPS;
                countForward -= FP_MAX_STEPS;
            }
            msg += countForward.ToString();
            countForward = 0;
        }
    }
    public static void SendPictureCoordinates(string msg) {
        if (toggleImageFlag) { client.SendMessage("D2", msg); }
    }

}
