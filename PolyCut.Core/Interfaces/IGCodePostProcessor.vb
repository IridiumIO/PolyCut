Public Interface IGCodePostProcessor
    Function Process(gcode As GCodeData, cfg As ProcessorConfiguration) As GCodeData
End Interface
