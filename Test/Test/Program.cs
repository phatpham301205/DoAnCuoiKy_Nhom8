using System;
using System.Collections.Generic;
using System.Threading;

class CrossyRoadSimpleBuffered
{
    // ===== Kích thước màn hình =====
    const int ConsoleWidth = 50;
    const int ConsoleHeight = 25;

    // ===== Ký tự hiển thị =====
    const char PlayerChar = 'P';
    const char RiverChar = 'R';
    const char LogChar = 'W';
    const char CrocodileChar = 'C';

    // ===== Thông số gameplay (dễ chỉnh) =====
    const int LogLength = 3;   // độ dài khúc gỗ
    const int CrocLength = 3;   // độ dài cá sấu
    const int PointsPerLevel = 1;   // mỗi 1 điểm thì lên 1 level
    const int SpeedStepMs = 20;  // mỗi lần lên level thì giảm 20ms/frame
    const int FrameMsStart = 120; // ms giữa 2 frame lúc đầu (100–140 là mượt)
    const int FrameMsMin = 60;  // nhanh nhất (đừng thấp quá sẽ giật)

    // ===== Trạng thái game =====
    static int playerX, playerY;
    static int score = 0, level = 1;
    static int frameMs = FrameMsStart;
    static bool isGameOver = false;

    // Bản đồ nền: cứ 4 hàng là một hàng sông (trừ mép trên/dưới)
    static List<List<char>> map = new List<List<char>>();

    // Vật thể động: (x, y, dir) — dir = -1 trái, +1 phải
    static List<Tuple<int, int, int>> logs = new List<Tuple<int, int, int>>();
    static List<Tuple<int, int, int>> crocs = new List<Tuple<int, int, int>>();

    static Random rng = new Random();

    // ===== Double buffer (giảm chớp) =====
    static char[,] prev; // khung hình trước
    static char[,] curr; // khung hình hiện tại

    static void Main()
    {
        Console.Title = "Crossy Road (Beginner + Double Buffer)";
        try
        {
            Console.SetWindowSize(ConsoleWidth, ConsoleHeight);
            Console.SetBufferSize(ConsoleWidth, ConsoleHeight);
        }
        catch { /* một số terminal giới hạn kích thước, bỏ qua */ }
        Console.CursorVisible = false;

        StartGame();

        while (!isGameOver)
        {
            HandleInput();
            UpdateGame();
            DrawGameBuffered(); // vẽ theo diff → mượt, ít chớp
            Thread.Sleep(frameMs);
        }

        GameOver();
    }

    static void StartGame()
    {
        playerX = ConsoleWidth / 2;
        playerY = ConsoleHeight - 2;
        score = 0;
        level = 1;
        frameMs = FrameMsStart;
        isGameOver = false;

        BuildMap();

        // Khởi tạo buffer
        prev = new char[ConsoleHeight, ConsoleWidth];
        curr = new char[ConsoleHeight, ConsoleWidth];
        for (int y = 0; y < ConsoleHeight; y++)
            for (int x = 0; x < ConsoleWidth; x++)
                prev[y, x] = '\0'; // ép vẽ toàn bộ ở frame đầu
    }

    // Tạo nền: hàng y chia hết 4 (trừ mép) là sông
    static void BuildMap()
    {
        map.Clear();
        for (int y = 0; y < ConsoleHeight; y++)
        {
            map.Add(new List<char>(ConsoleWidth));
            bool isRiver = (y % 4 == 0 && y > 0 && y < ConsoleHeight - 1);
            for (int x = 0; x < ConsoleWidth; x++)
                map[y].Add(isRiver ? RiverChar : ' ');
        }
        logs.Clear();
        crocs.Clear();
    }

    // Điều khiển: ↑ ↓ ← →
    static void HandleInput()
    {
        while (Console.KeyAvailable)
        {
            var k = Console.ReadKey(true).Key;
            if (k == ConsoleKey.UpArrow) playerY--;
            else if (k == ConsoleKey.DownArrow) playerY++;
            else if (k == ConsoleKey.LeftArrow) playerX--;
            else if (k == ConsoleKey.RightArrow) playerX++;
            else if (k == ConsoleKey.Escape) isGameOver = true;
        }
    }

    static void UpdateGame()
    {
        // Giới hạn biên
        if (playerX < 0) playerX = 0;
        if (playerX >= ConsoleWidth) playerX = ConsoleWidth - 1;
        if (playerY < 0) playerY = 0;
        if (playerY >= ConsoleHeight) playerY = ConsoleHeight - 1;

        // Di chuyển khúc gỗ
        for (int i = 0; i < logs.Count; i++)
        {
            var t = logs[i];
            int newX = (t.Item1 + t.Item3 + ConsoleWidth) % ConsoleWidth;
            logs[i] = Tuple.Create(newX, t.Item2, t.Item3);
        }

        // Di chuyển cá sấu
        for (int i = 0; i < crocs.Count; i++)
        {
            var t = crocs[i];
            int newX = (t.Item1 + t.Item3 + ConsoleWidth) % ConsoleWidth;
            crocs[i] = Tuple.Create(newX, t.Item2, t.Item3);
        }

        // Spawn thêm theo độ khó (điểm càng cao: C nhiều, W ít)
        for (int y = 4; y < ConsoleHeight; y += 4)
        {
            int maxLogs = Math.Max(1, 5 - score / 10);
            int maxCrocs = Math.Min(6, 1 + score / 10);

            if (CountAtRow(logs, y) < maxLogs && rng.Next(10) < 2)
            {
                int dir = rng.Next(2) == 0 ? -1 : 1;
                int spawnX = (dir == 1) ? 0 : ConsoleWidth - 1;
                logs.Add(Tuple.Create(spawnX, y, dir));
            }
            if (CountAtRow(crocs, y) < maxCrocs && rng.Next(20) < 2)
            {
                int dir = rng.Next(2) == 0 ? -1 : 1;
                int spawnX = (dir == 1) ? 0 : ConsoleWidth - 1;
                crocs.Add(Tuple.Create(spawnX, y, dir));
            }
        }

        // Va chạm với cá sấu (cá sấu dài CrocLength ô)
        foreach (var c in crocs)
        {
            int dir = c.Item3;
            for (int k = 0; k < CrocLength; k++)
            {
                int cx = c.Item1 + k * dir;
                if (c.Item2 == playerY && cx == playerX)
                {
                    isGameOver = true;
                    return;
                }
            }
        }

        // Nếu đang ở hàng sông: phải đứng trên khúc gỗ (khúc gỗ dài LogLength ô)
        bool onRiverRow = (playerY % 4 == 0 && playerY > 0 && playerY < ConsoleHeight - 1);
        if (onRiverRow)
        {
            bool onLog = false;
            foreach (var w in logs)
            {
                int dir = w.Item3;
                for (int k = 0; k < LogLength; k++)
                {
                    int lx = w.Item1 + k * dir;
                    if (w.Item2 == playerY && lx == playerX)
                    {
                        onLog = true;
                        // Trôi theo khúc gỗ
                        playerX += dir;
                        if (playerX < 0 || playerX >= ConsoleWidth)
                        {
                            isGameOver = true; // trôi ra khỏi màn
                            return;
                        }
                        break;
                    }
                }
                if (onLog) break;
            }
            if (!onLog)
            {
                isGameOver = true; // rơi xuống nước
                return;
            }
        }

        // Qua màn (đến sát mép trên)
        if (playerY < 1)
        {
            score++;

            // Lên level theo điểm
            int newLevel = (score / PointsPerLevel) + 1;
            if (newLevel > level)
            {
                level = newLevel;
                frameMs = Math.Max(FrameMsMin, frameMs - SpeedStepMs); // nhanh dần nhưng có ngưỡng
            }

            // Reset vị trí + tái tạo map/đối tượng
            playerY = ConsoleHeight - 2;
            BuildMap();
        }
    }

    static int CountAtRow(List<Tuple<int, int, int>> list, int row)
    {
        int c = 0;
        foreach (var t in list) if (t.Item2 == row) c++;
        return c;
    }

    // ===== Vẽ theo "diff" giữa curr và prev để giảm chớp =====
    static void DrawGameBuffered()
    {
        // 1) Dựng toàn bộ khung hình vào curr (chưa in ra)
        // Nền
        for (int y = 0; y < ConsoleHeight; y++)
            for (int x = 0; x < ConsoleWidth; x++)
                curr[y, x] = (map[y][x] == RiverChar) ? RiverChar : ' ';

        // Khúc gỗ dài
        foreach (var w in logs)
        {
            int dir = w.Item3;
            for (int k = 0; k < LogLength; k++)
            {
                int x = w.Item1 + k * dir, y = w.Item2;
                if (x >= 0 && x < ConsoleWidth && y >= 0 && y < ConsoleHeight)
                    curr[y, x] = LogChar;
            }
        }

        // Cá sấu dài
        foreach (var c in crocs)
        {
            int dir = c.Item3;
            for (int k = 0; k < CrocLength; k++)
            {
                int x = c.Item1 + k * dir, y = c.Item2;
                if (x >= 0 && x < ConsoleWidth && y >= 0 && y < ConsoleHeight)
                    curr[y, x] = CrocodileChar;
            }
        }

        // Người chơi
        if (playerX >= 0 && playerX < ConsoleWidth && playerY >= 0 && playerY < ConsoleHeight)
            curr[playerY, playerX] = PlayerChar;

        // HUD: Speed Level = (FrameMsStart - frameMs) / SpeedStepMs + 1
        int speedLevel = (FrameMsStart - frameMs) / SpeedStepMs + 1;
        string hud = $"Score: {score} | Level: {level} | Speed: {speedLevel}   ";
        int hudStartX = 1;
        for (int i = 0; i < hud.Length && hudStartX + i < ConsoleWidth; i++)
            curr[0, hudStartX + i] = hud[i];


        // 2) Chỉ in những ô khác với prev
        for (int y = 0; y < ConsoleHeight; y++)
        {
            for (int x = 0; x < ConsoleWidth; x++)
            {
                if (curr[y, x] != prev[y, x])
                {
                    Console.SetCursorPosition(x, y);
                    SetColorFor(curr[y, x]);
                    Console.Write(curr[y, x]);
                    prev[y, x] = curr[y, x];
                }
            }
        }
    }

    static void SetColorFor(char ch)
    {
        // Màu theo nội dung ô
        if (ch == RiverChar) Console.ForegroundColor = ConsoleColor.Blue;
        else if (ch == LogChar) Console.ForegroundColor = ConsoleColor.Yellow;
        else if (ch == CrocodileChar) Console.ForegroundColor = ConsoleColor.Red;
        else if (ch == PlayerChar) Console.ForegroundColor = ConsoleColor.White;
        else if (ch == ' ') Console.ForegroundColor = ConsoleColor.Green; // nền đất
        else Console.ForegroundColor = ConsoleColor.White; // HUD & ký tự khác
    }

    static void GameOver()
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.SetCursorPosition(ConsoleWidth / 2 - 5, ConsoleHeight / 2 - 1);
        Console.Write("GAME OVER!");
        Console.ForegroundColor = ConsoleColor.White;
        Console.SetCursorPosition(ConsoleWidth / 2 - 10, ConsoleHeight / 2 + 1);
        Console.Write($"Final Score: {score} | Level: {level}");
        Console.SetCursorPosition(ConsoleWidth / 2 - 12, ConsoleHeight / 2 + 3);
        Console.Write("Press any key to exit...");
        Console.ReadKey(true);
    }
}

