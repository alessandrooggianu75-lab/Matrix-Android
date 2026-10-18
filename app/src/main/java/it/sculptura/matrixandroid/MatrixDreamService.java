package it.sculptura.matrixandroid;

import android.service.dreams.DreamService;

public class MatrixDreamService extends DreamService {
    @Override public void onAttachedToWindow() {
        super.onAttachedToWindow();
        setInteractive(false);
        setFullscreen(true);
        setScreenBright(false);
        setContentView(new MatrixRainView(this));
    }
}
