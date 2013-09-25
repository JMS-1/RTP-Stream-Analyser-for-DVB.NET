RTP Stream Analyser for DVB.NET
===============================

This sample shows the use of the DVB.NET classes for parsing a Transport Stream as a whole (TSParser) and
individual SI tables (EPG/EIT, PAT, PMT, ...). In addition it introduces the double buffered file writer
used by VCR.NET to decouple recording from persisting the result.

The sample is fed by an RTP network stream - although the parsing is rather simplistic and will generate
data loss in production scenarios (e.g. extensions and padding will lead to skipped packages). But using
VLC to generate the RTP stream it seems to work fine. In fact the sample can easily be modified to take
the input from anywhere - e.g. some pre-recorded file. The source is not the concern here.
