using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]
    [SerializeField]
    private HomePageController homePageController;

    [SerializeField]
    private GazeRecorder gazeRecorder;


    // ============================================================
    // STUDY SETTINGS
    // ============================================================

    [Header("Study Settings")]
    [SerializeField]
    private string studyId = "VRET_HTC";

    [SerializeField]
    private string outputFolderName = "ImmersiveEyeData";


    // ============================================================
    // RUNTIME STATUS
    // ============================================================

    [Header("Runtime Status")]
    [SerializeField]
    private string participantId = "";

    [SerializeField]
    private string sessionId = "";

    [SerializeField]
    private string lastSavedFilePath = "";

    [SerializeField]
    private int lastSavedSampleCount = 0;

    [SerializeField]
    private string lastSavedBlockName = "";


    // ============================================================
    // PUBLIC ACCESS
    // ============================================================

    public string ParticipantId =>
        participantId;

    public string SessionId =>
        sessionId;

    public string LastSavedFilePath =>
        lastSavedFilePath;

    public int LastSavedSampleCount =>
        lastSavedSampleCount;

    public string LastSavedBlockName =>
        lastSavedBlockName;


    // ============================================================
    // SAVE CURRENT GAZE DATA
    // ============================================================

    public void SaveCurrentGazeData()
    {
        if (homePageController == null)
        {
            Debug.LogWarning(
                "DataManager: HomePageController is not assigned."
            );

            return;
        }

        if (gazeRecorder == null)
        {
            Debug.LogWarning(
                "DataManager: GazeRecorder is not assigned."
            );

            return;
        }


        // --------------------------------------------------------
        // PARTICIPANT / SESSION
        // --------------------------------------------------------

        participantId =
            SanitizeIdentifier(
                homePageController.GetParticipantID()
            );

        sessionId =
            SanitizeIdentifier(
                homePageController.GetSessionID()
            );


        if (string.IsNullOrWhiteSpace(participantId))
        {
            Debug.LogWarning(
                "DataManager: Participant ID is empty."
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Debug.LogWarning(
                "DataManager: Session ID is empty."
            );

            return;
        }


        // --------------------------------------------------------
        // GAZE SAMPLES
        // --------------------------------------------------------

        IReadOnlyList<GazeRecorder.GazeSample> samples =
            gazeRecorder.Samples;


        if (samples == null || samples.Count == 0)
        {
            Debug.LogWarning(
                "DataManager: No gaze samples are available to save."
            );

            return;
        }


        // --------------------------------------------------------
        // DETERMINE BLOCK NAME AUTOMATICALLY
        // --------------------------------------------------------

        string blockName =
            samples[0].blockName;

        if (string.IsNullOrWhiteSpace(blockName))
        {
            blockName =
                gazeRecorder.BlockName;
        }

        if (string.IsNullOrWhiteSpace(blockName))
        {
            blockName =
                "UnknownBlock";
        }

        blockName =
            SanitizeIdentifier(
                blockName
            );

        lastSavedBlockName =
            blockName;


        // --------------------------------------------------------
        // OUTPUT DIRECTORY
        // --------------------------------------------------------

        string participantFolder =
            Path.Combine(
                Application.persistentDataPath,
                outputFolderName,
                participantId
            );

        Directory.CreateDirectory(
            participantFolder
        );


        // --------------------------------------------------------
        // FILE NAME
        // --------------------------------------------------------

        string baseFileName =
            participantId
            + "_"
            + sessionId
            + "_"
            + blockName
            + "_raw_gaze.csv";


        string requestedPath =
            Path.Combine(
                participantFolder,
                baseFileName
            );


        string finalPath =
            GenerateUniqueFilePath(
                requestedPath
            );


        // --------------------------------------------------------
        // BUILD CSV
        // --------------------------------------------------------

        StringBuilder csv =
            new StringBuilder();


        AppendHeader(
            csv
        );


        foreach (
            GazeRecorder.GazeSample sample
            in samples
        )
        {
            AppendSample(
                csv,
                sample
            );
        }


        // --------------------------------------------------------
        // SAVE
        // --------------------------------------------------------

        File.WriteAllText(
            finalPath,
            csv.ToString(),
            Encoding.UTF8
        );


        lastSavedFilePath =
            finalPath;

        lastSavedSampleCount =
            samples.Count;


        Debug.Log(
            "GAZE_DATA_SAVED"
            + " | Block = "
            + blockName
            + " | Samples = "
            + samples.Count
            + " | File = "
            + finalPath
        );
    }


    // ============================================================
    // CSV HEADER
    // ============================================================

    private void AppendHeader(
        StringBuilder csv
    )
    {
        csv.AppendLine(
            "study_id,"
            + "participant_id,"
            + "session_id,"
            + "block_name,"
            + "stimulus_name,"
            + "sample_index,"
            + "timestamp_utc,"
            + "unity_time_s,"
            + "recording_elapsed_s,"
            + "frame_number,"
            + "left_gaze_valid,"
            + "right_gaze_valid,"
            + "combined_gaze_valid,"
            + "left_gaze_origin_x,"
            + "left_gaze_origin_y,"
            + "left_gaze_origin_z,"
            + "left_gaze_direction_x,"
            + "left_gaze_direction_y,"
            + "left_gaze_direction_z,"
            + "right_gaze_origin_x,"
            + "right_gaze_origin_y,"
            + "right_gaze_origin_z,"
            + "right_gaze_direction_x,"
            + "right_gaze_direction_y,"
            + "right_gaze_direction_z,"
            + "combined_gaze_origin_x,"
            + "combined_gaze_origin_y,"
            + "combined_gaze_origin_z,"
            + "combined_gaze_direction_x,"
            + "combined_gaze_direction_y,"
            + "combined_gaze_direction_z,"
            + "left_pupil_diameter_valid,"
            + "left_pupil_diameter,"
            + "right_pupil_diameter_valid,"
            + "right_pupil_diameter,"
            + "left_pupil_position_valid,"
            + "left_pupil_position_x,"
            + "left_pupil_position_y,"
            + "right_pupil_position_valid,"
            + "right_pupil_position_x,"
            + "right_pupil_position_y,"
            + "left_eye_geometry_valid,"
            + "left_eye_openness,"
            + "left_eye_squeeze,"
            + "left_eye_wide,"
            + "right_eye_geometry_valid,"
            + "right_eye_openness,"
            + "right_eye_squeeze,"
            + "right_eye_wide,"
            + "head_position_x,"
            + "head_position_y,"
            + "head_position_z,"
            + "head_rotation_x,"
            + "head_rotation_y,"
            + "head_rotation_z,"
            + "head_rotation_w,"
            + "gaze_hit,"
            + "hit_object_name,"
            + "hit_position_x,"
            + "hit_position_y,"
            + "hit_position_z,"
            + "hit_distance,"
            + "aoi_found,"
            + "aoi_name,"
            + "aoi_category,"
            + "aoi_id"
        );
    }


    // ============================================================
    // CSV SAMPLE
    // ============================================================

    private void AppendSample(
        StringBuilder csv,
        GazeRecorder.GazeSample sample
    )
    {
        List<string> row =
            new List<string>();


        row.Add(
            EscapeCsv(studyId)
        );

        row.Add(
            EscapeCsv(participantId)
        );

        row.Add(
            EscapeCsv(sessionId)
        );

        row.Add(
            EscapeCsv(sample.blockName)
        );

        row.Add(
            EscapeCsv(sample.stimulusName)
        );

        row.Add(
            sample.sampleIndex.ToString(
                CultureInfo.InvariantCulture
            )
        );

        row.Add(
            EscapeCsv(sample.timestampUtc)
        );

        row.Add(
            FloatToString(sample.unityTime)
        );

        row.Add(
            FloatToString(
                sample.recordingElapsedTime
            )
        );

        row.Add(
            sample.frameNumber.ToString(
                CultureInfo.InvariantCulture
            )
        );


        // --------------------------------------------------------
        // GAZE VALIDITY
        // --------------------------------------------------------

        row.Add(
            BoolToString(sample.leftGazeValid)
        );

        row.Add(
            BoolToString(sample.rightGazeValid)
        );

        row.Add(
            BoolToString(sample.combinedGazeValid)
        );


        // --------------------------------------------------------
        // GAZE ORIGIN / DIRECTION
        // --------------------------------------------------------

        AddVector3(
            row,
            sample.leftGazeOriginWorld
        );

        AddVector3(
            row,
            sample.leftGazeDirectionWorld
        );

        AddVector3(
            row,
            sample.rightGazeOriginWorld
        );

        AddVector3(
            row,
            sample.rightGazeDirectionWorld
        );

        AddVector3(
            row,
            sample.combinedGazeOriginWorld
        );

        AddVector3(
            row,
            sample.combinedGazeDirectionWorld
        );


        // --------------------------------------------------------
        // PUPIL
        // --------------------------------------------------------

        row.Add(
            BoolToString(
                sample.leftPupilDiameterValid
            )
        );

        row.Add(
            FloatToString(
                sample.leftPupilDiameter
            )
        );

        row.Add(
            BoolToString(
                sample.rightPupilDiameterValid
            )
        );

        row.Add(
            FloatToString(
                sample.rightPupilDiameter
            )
        );


        row.Add(
            BoolToString(
                sample.leftPupilPositionValid
            )
        );

        AddVector2(
            row,
            sample.leftPupilPosition
        );


        row.Add(
            BoolToString(
                sample.rightPupilPositionValid
            )
        );

        AddVector2(
            row,
            sample.rightPupilPosition
        );


        // --------------------------------------------------------
        // EYE GEOMETRY
        // --------------------------------------------------------

        row.Add(
            BoolToString(
                sample.leftEyeGeometryValid
            )
        );

        row.Add(
            FloatToString(
                sample.leftEyeOpenness
            )
        );

        row.Add(
            FloatToString(
                sample.leftEyeSqueeze
            )
        );

        row.Add(
            FloatToString(
                sample.leftEyeWide
            )
        );


        row.Add(
            BoolToString(
                sample.rightEyeGeometryValid
            )
        );

        row.Add(
            FloatToString(
                sample.rightEyeOpenness
            )
        );

        row.Add(
            FloatToString(
                sample.rightEyeSqueeze
            )
        );

        row.Add(
            FloatToString(
                sample.rightEyeWide
            )
        );


        // --------------------------------------------------------
        // HEAD POSE
        // --------------------------------------------------------

        AddVector3(
            row,
            sample.headPosition
        );

        row.Add(
            FloatToString(
                sample.headRotation.x
            )
        );

        row.Add(
            FloatToString(
                sample.headRotation.y
            )
        );

        row.Add(
            FloatToString(
                sample.headRotation.z
            )
        );

        row.Add(
            FloatToString(
                sample.headRotation.w
            )
        );


        // --------------------------------------------------------
        // HIT DATA
        // --------------------------------------------------------

        row.Add(
            BoolToString(
                sample.gazeHit
            )
        );

        row.Add(
            EscapeCsv(
                sample.hitObjectName
            )
        );

        AddVector3(
            row,
            sample.hitPosition
        );

        row.Add(
            FloatToString(
                sample.hitDistance
            )
        );


        // --------------------------------------------------------
        // AOI DATA
        // --------------------------------------------------------

        row.Add(
            BoolToString(
                sample.aoiFound
            )
        );

        row.Add(
            EscapeCsv(
                sample.aoiName
            )
        );

        row.Add(
            EscapeCsv(
                sample.aoiCategory
            )
        );

        row.Add(
            EscapeCsv(
                sample.aoiId
            )
        );


        csv.AppendLine(
            string.Join(
                ",",
                row
            )
        );
    }


    // ============================================================
    // VECTOR HELPERS
    // ============================================================

    private void AddVector3(
        List<string> row,
        Vector3 value
    )
    {
        row.Add(
            FloatToString(value.x)
        );

        row.Add(
            FloatToString(value.y)
        );

        row.Add(
            FloatToString(value.z)
        );
    }


    private void AddVector2(
        List<string> row,
        Vector2 value
    )
    {
        row.Add(
            FloatToString(value.x)
        );

        row.Add(
            FloatToString(value.y)
        );
    }


    // ============================================================
    // FORMAT HELPERS
    // ============================================================

    private string FloatToString(
        float value
    )
    {
        if (
            float.IsNaN(value)
            ||
            float.IsInfinity(value)
        )
        {
            return "";
        }

        return value.ToString(
            "G9",
            CultureInfo.InvariantCulture
        );
    }


    private string BoolToString(
        bool value
    )
    {
        return value
            ? "1"
            : "0";
    }


    private string EscapeCsv(
        string value
    )
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        bool requiresQuotes =
            value.Contains(",")
            ||
            value.Contains("\"")
            ||
            value.Contains("\n")
            ||
            value.Contains("\r");

        if (!requiresQuotes)
        {
            return value;
        }

        return
            "\""
            +
            value.Replace(
                "\"",
                "\"\""
            )
            +
            "\"";
    }


    // ============================================================
    // SANITIZE FILE / ID STRINGS
    // ============================================================

    private string SanitizeIdentifier(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        string trimmed =
            value.Trim();

        foreach (
            char invalidCharacter
            in Path.GetInvalidFileNameChars()
        )
        {
            trimmed =
                trimmed.Replace(
                    invalidCharacter,
                    '_'
                );
        }

        return trimmed;
    }


    // ============================================================
    // NEVER OVERWRITE AN EXISTING SESSION
    // ============================================================

    private string GenerateUniqueFilePath(
        string requestedPath
    )
    {
        if (!File.Exists(requestedPath))
        {
            return requestedPath;
        }

        string directory =
            Path.GetDirectoryName(
                requestedPath
            );

        string fileName =
            Path.GetFileNameWithoutExtension(
                requestedPath
            );

        string extension =
            Path.GetExtension(
                requestedPath
            );

        int version =
            2;

        while (true)
        {
            string candidate =
                Path.Combine(
                    directory,
                    fileName
                    + "_"
                    + version.ToString("D2")
                    + extension
                );

            if (!File.Exists(candidate))
            {
                return candidate;
            }

            version++;
        }
    }
}