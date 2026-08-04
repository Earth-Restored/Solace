package earthrestored.solace.eventbus.client;

import java.io.IOException;
import java.io.InputStream;
import java.io.InterruptedIOException;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.LinkedBlockingQueue;

public class ChunkedInputStream extends InputStream {
    private final BlockingQueue<byte[]> queue = new LinkedBlockingQueue<>();
    private byte[] current;
    private int pos = 0;
    private boolean eof = false;
    private Throwable error = null;

    public void push(byte[] data) {
        queue.add(data);
    }

    public void complete() {
        queue.add(new byte[0]);
    }

    public void error(Throwable t) {
        this.error = t;
        complete();
    }

    @Override
    public int read(byte[] b, int off, int len) throws IOException {
        if (eof && current == null)
            return -1;

        if (current == null || pos >= current.length) {
            try {
                current = queue.take();
                pos = 0;
                if (error != null)
                    throw new IOException(error);
                if (current.length == 0) {
                    eof = true;
                    current = null;
                    return -1;
                }
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
                throw new InterruptedIOException();
            }
        }

        int available = current.length - pos;
        int toRead = Math.min(len, available);
        System.arraycopy(current, pos, b, off, toRead);
        pos += toRead;
        return toRead;
    }

    @Override
    public int read() throws IOException {
        byte[] b = new byte[1];
        int r = read(b, 0, 1);
        return r == -1 ? -1 : (b[0] & 0xFF);
    }
}
